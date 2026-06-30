"use client";
import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import toast from "react-hot-toast";
import {
  fetchHabits, createHabit, updateHabit, deleteHabit, getSubscriptionStatus,
} from "../../../../lib/api";
import type { Habit, SubscriptionStatus } from "../../../../lib/types";

const ICONS  = ["✅", "💪", "📚", "🏃", "💧", "🧘", "🥗", "😴", "🎯", "🎨"];
const COLORS = ["#3b82f6", "#10b981", "#f59e0b", "#ef4444", "#8b5cf6", "#ec4899", "#06b6d4", "#f97316"];
const FREE_LIMIT = 3;

function HabitForm({ habit, onSave, onCancel }: {
  habit?: Habit;
  onSave: (name: string, color: string, icon: string) => Promise<void>;
  onCancel: () => void;
}) {
  const [name,    setName]    = useState(habit?.name  ?? "");
  const [icon,    setIcon]    = useState(habit?.icon  ?? "✅");
  const [color,   setColor]   = useState(habit?.color ?? "#3b82f6");
  const [saving,  setSaving]  = useState(false);
  const [error,   setError]   = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;
    setSaving(true);
    setError("");
    try {
      await onSave(name.trim(), color, icon);
    } catch (err) {
      setError((err as Error).message ?? "Save failed");
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="bg-white border border-gray-200 rounded-xl p-5 space-y-4">
      <div>
        <label className="block text-xs font-semibold text-gray-500 uppercase tracking-wider mb-1">Name</label>
        <input
          autoFocus required value={name} onChange={(e) => setName(e.target.value)}
          placeholder="e.g. Read 30 minutes"
          className="w-full border border-gray-300 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div>
        <label className="block text-xs font-semibold text-gray-500 uppercase tracking-wider mb-2">Icon</label>
        <div className="flex flex-wrap gap-2">
          {ICONS.map((i) => (
            <button key={i} type="button" onClick={() => setIcon(i)}
              className={`w-10 h-10 rounded-xl text-xl flex items-center justify-center transition ${
                icon === i ? "bg-blue-100 ring-2 ring-blue-500" : "bg-gray-100 hover:bg-gray-200"
              }`}
            >{i}</button>
          ))}
        </div>
      </div>

      <div>
        <label className="block text-xs font-semibold text-gray-500 uppercase tracking-wider mb-2">Color</label>
        <div className="flex gap-2">
          {COLORS.map((c) => (
            <button key={c} type="button" onClick={() => setColor(c)}
              className={`w-8 h-8 rounded-full transition ${color === c ? "ring-4 ring-offset-1 ring-gray-400" : ""}`}
              style={{ backgroundColor: c }}
            />
          ))}
        </div>
      </div>

      {error && <p className="text-red-500 text-sm">{error}</p>}

      <div className="flex gap-3 pt-1">
        <button type="button" onClick={onCancel}
          className="flex-1 border border-gray-200 text-gray-600 font-medium py-2.5 rounded-xl text-sm hover:bg-gray-50 transition"
        >Cancel</button>
        <button type="submit" disabled={!name.trim() || saving}
          className="flex-1 bg-blue-600 hover:bg-blue-700 text-white font-semibold py-2.5 rounded-xl text-sm transition disabled:opacity-50"
        >{saving ? "Saving…" : habit ? "Save changes" : "Add habit"}</button>
      </div>
    </form>
  );
}

export default function ManageHabitsPage() {
  const router = useRouter();
  const [habits,   setHabits]   = useState<Habit[]>([]);
  const [sub,      setSub]      = useState<SubscriptionStatus | null>(null);
  const [loading,  setLoading]  = useState(true);
  const [adding,   setAdding]   = useState(false);
  const [editing,  setEditing]  = useState<string | null>(null);
  const [deleting, setDeleting] = useState<string | null>(null);

  const load = useCallback(async () => {
    const [h, s] = await Promise.all([fetchHabits(), getSubscriptionStatus()]);
    setHabits(h); setSub(s); setLoading(false);
  }, []);

  useEffect(() => { load(); }, [load]);

  const isActive    = sub?.status === "Active" || sub?.status === "Trialing";
  const atFreeLimit = !isActive && habits.length >= FREE_LIMIT;

  const handleAdd = async (name: string, color: string, icon: string) => {
    try {
      const h = await createHabit(name, color, icon);
      setHabits((prev) => [...prev, h]);
      setAdding(false);
    } catch (err) {
      const msg = (err as Error).message ?? "";
      if (msg.includes("[403]")) { router.push("/subscription"); return; }
      throw err;
    }
  };

  const handleEdit = async (id: string, name: string, color: string, icon: string) => {
    const h = await updateHabit(id, name, color, icon);
    setHabits((prev) => prev.map((x) => x.id === id ? h : x));
    setEditing(null);
  };

  const handleDelete = async (id: string) => {
    if (!confirm("Delete this habit and all its completions?")) return;
    setDeleting(id);
    try {
      await deleteHabit(id);
      setHabits((prev) => prev.filter((h) => h.id !== id));
    } catch {
      toast.error("Failed to delete habit. Please try again.");
    } finally {
      setDeleting(null);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="w-8 h-8 border-4 border-blue-600 border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-gray-900">My Habits</h1>
        {!atFreeLimit && !adding && (
          <button
            onClick={() => setAdding(true)}
            className="bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold px-4 py-2 rounded-xl transition"
          >+ Add habit</button>
        )}
      </div>

      {atFreeLimit && (
        <Link href="/subscription" className="block mb-5 bg-amber-50 border border-amber-200 rounded-xl px-4 py-3 hover:bg-amber-100 transition">
          <p className="text-amber-700 font-semibold text-sm">🔒 Free plan limit reached</p>
          <p className="text-amber-600 text-xs mt-0.5">Upgrade to Pro to add unlimited habits</p>
        </Link>
      )}

      {adding && (
        <div className="mb-5">
          <HabitForm onSave={handleAdd} onCancel={() => setAdding(false)} />
        </div>
      )}

      {habits.length === 0 && !adding ? (
        <div className="text-center py-16">
          <p className="text-5xl mb-3">✨</p>
          <p className="text-gray-900 font-bold text-lg mb-1">No habits yet</p>
          <p className="text-gray-400 text-sm mb-4">Add your first habit above</p>
        </div>
      ) : (
        <div className="space-y-3">
          {habits.map((h) => (
            <div key={h.id}>
              {editing === h.id ? (
                <HabitForm
                  habit={h}
                  onSave={(name, color, icon) => handleEdit(h.id, name, color, icon)}
                  onCancel={() => setEditing(null)}
                />
              ) : (
                <div className="flex items-center gap-3 bg-white border border-gray-200 rounded-xl px-4 py-3.5">
                  <div className="w-1 h-10 rounded-full flex-shrink-0" style={{ backgroundColor: h.color }} />
                  <span className="text-2xl">{h.icon}</span>
                  <span className="flex-1 font-medium text-gray-900">{h.name}</span>
                  <button
                    onClick={() => setEditing(h.id)}
                    className="text-sm text-blue-600 hover:text-blue-700 font-medium px-2"
                  >Edit</button>
                  <button
                    onClick={() => handleDelete(h.id)}
                    disabled={deleting === h.id}
                    className="text-sm text-red-500 hover:text-red-600 font-medium px-2 disabled:opacity-50"
                  >{deleting === h.id ? "…" : "Delete"}</button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
