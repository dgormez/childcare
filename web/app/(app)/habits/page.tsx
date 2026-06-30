"use client";
import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import toast from "react-hot-toast";
import { fetchHabits, fetchCompletions, completeHabit, uncompleteHabit, getSubscriptionStatus } from "../../../lib/api";
import type { Habit, HabitCompletion, SubscriptionStatus } from "../../../lib/types";

function getToday() {
  return new Date().toISOString().slice(0, 10);
}

function getTodayLabel() {
  return new Date().toLocaleDateString("en-US", { weekday: "long", month: "long", day: "numeric" });
}

function TrialBanner({ sub }: { sub: SubscriptionStatus }) {
  const daysLeft = sub.currentPeriodEnd
    ? Math.max(0, Math.ceil((new Date(sub.currentPeriodEnd).getTime() - Date.now()) / 86400000))
    : null;

  return (
    <Link href="/subscription" className="block mb-6 bg-blue-50 border border-blue-200 rounded-xl px-4 py-3 hover:bg-blue-100 transition">
      <p className="text-blue-700 font-semibold text-sm">⏳ Free Trial Active</p>
      <p className="text-blue-600 text-xs mt-0.5">
        {daysLeft != null ? `${daysLeft} day${daysLeft !== 1 ? "s" : ""} remaining` : "Trial ending soon"} · Click to upgrade
      </p>
    </Link>
  );
}

export default function TodayPage() {
  const [today,       setToday]       = useState(getToday);
  const [habits,      setHabits]      = useState<Habit[]>([]);
  const [completions, setCompletions] = useState<HabitCompletion[]>([]);
  const [sub,         setSub]         = useState<SubscriptionStatus | null>(null);
  const [loading,     setLoading]     = useState(true);

  // Re-schedule at each midnight so a browser left open overnight stays correct
  useEffect(() => {
    const now = new Date();
    const msUntilMidnight = +new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1) - +now;
    const timer = setTimeout(() => setToday(getToday()), msUntilMidnight);
    return () => clearTimeout(timer);
  }, [today]);

  const load = useCallback(async () => {
    const [h, c, s] = await Promise.all([
      fetchHabits(),
      fetchCompletions(today, today),
      getSubscriptionStatus(),
    ]);
    setHabits(h);
    setCompletions(c);
    setSub(s);
    setLoading(false);
  }, [today]);

  useEffect(() => { load(); }, [load]);

  const completedIds = new Set(completions.map((c) => c.habitId));
  const doneCount    = habits.filter((h) => completedIds.has(h.id)).length;

  const toggle = async (habit: Habit) => {
    if (completedIds.has(habit.id)) {
      // Optimistic remove — save the existing record so we can roll back
      const existing = completions.find((c) => c.habitId === habit.id);
      setCompletions((prev) => prev.filter((c) => c.habitId !== habit.id));
      try {
        await uncompleteHabit(habit.id, today);
      } catch {
        if (existing) setCompletions((prev) => [...prev, existing]);
        toast.error("Failed to update. Please try again.");
      }
    } else {
      try {
        const c = await completeHabit(habit.id, today);
        setCompletions((prev) => [...prev, c]);
      } catch {
        toast.error("Failed to update. Please try again.");
      }
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
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Today</h1>
          <p className="text-gray-500 text-sm mt-0.5">{getTodayLabel()}</p>
        </div>
        <Link
          href="/habits/manage"
          className="bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold px-4 py-2 rounded-xl transition"
        >
          Manage habits
        </Link>
      </div>

      {sub?.status === "Trialing" && <TrialBanner sub={sub} />}

      {habits.length > 0 && (
        <div className="mb-6">
          <div className="flex justify-between text-xs text-gray-400 mb-1">
            <span>{doneCount} of {habits.length} done</span>
            <span>{habits.length > 0 ? Math.round((doneCount / habits.length) * 100) : 0}%</span>
          </div>
          <div className="h-2 bg-gray-200 rounded-full overflow-hidden">
            <div
              className="h-full bg-green-500 rounded-full transition-all"
              style={{ width: `${habits.length > 0 ? (doneCount / habits.length) * 100 : 0}%` }}
            />
          </div>
        </div>
      )}

      {habits.length === 0 ? (
        <div className="text-center py-20">
          <p className="text-5xl mb-4">🌱</p>
          <h2 className="text-xl font-bold text-gray-900 mb-2">No habits yet</h2>
          <p className="text-gray-400 text-sm mb-6">Start building better habits today</p>
          <Link href="/habits/manage" className="bg-blue-600 text-white font-semibold px-6 py-3 rounded-xl">
            Add your first habit
          </Link>
        </div>
      ) : (
        <div className="space-y-3">
          {habits.map((h) => {
            const done = completedIds.has(h.id);
            return (
              <button
                key={h.id}
                onClick={() => toggle(h)}
                className="w-full flex items-center gap-4 bg-white border border-gray-200 rounded-xl px-4 py-4 hover:border-gray-300 transition text-left"
              >
                <div className={`w-7 h-7 rounded-full flex items-center justify-center border-2 flex-shrink-0 ${
                  done ? "bg-green-500 border-green-500" : "border-gray-300"
                }`}>
                  {done && <span className="text-white text-xs font-bold">✓</span>}
                </div>
                <span className="text-xl">{h.icon}</span>
                <span className={`flex-1 font-medium ${done ? "text-gray-400 line-through" : "text-gray-900"}`}>
                  {h.name}
                </span>
                <div className="w-3 h-3 rounded-full flex-shrink-0" style={{ backgroundColor: h.color }} />
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
