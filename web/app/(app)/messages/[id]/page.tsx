"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { ArrowLeft } from "lucide-react";
import { apiClient } from "../../../../lib/apiClient";
import { Button } from "../../../../components/ui/button";
import { ErrorState } from "../../../../components/ErrorState";
import type { MessageThreadResponse } from "../../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

export default function MessageThreadPage() {
  const t = useTranslations("messages");
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const [thread, setThread] = useState<MessageThreadResponse | null>(null);
  const [state, setState] = useState<LoadState>("loading");
  const [reply, setReply] = useState("");
  const [sending, setSending] = useState(false);

  const load = useCallback(async () => {
    setState("loading");
    const result = await (apiClient.GET as any)("/api/message-threads/{id}", { params: { path: { id: params.id } } });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setThread(result.data as MessageThreadResponse);
    setState("loaded");
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  async function sendReply() {
    if (!reply.trim()) return;
    setSending(true);
    const result = await (apiClient.POST as any)("/api/message-threads/{id}/messages", {
      params: { path: { id: params.id } },
      body: { body: reply.trim() },
    });
    setSending(false);
    if (!result.response.ok) return;
    setReply("");
    await load();
  }

  if (state === "loading") return <div className="h-64 rounded-lg bg-surface-soft dark:bg-surface-soft-dark" />;
  if (state === "error" || !thread) return <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />;

  return (
    <div className="mx-auto max-w-2xl">
      <button
        onClick={() => router.push("/messages")}
        className="mb-4 flex items-center gap-2 text-sm text-text-soft hover:text-text dark:text-text-soft-dark dark:hover:text-text-dark"
      >
        <ArrowLeft className="h-4 w-4" strokeWidth={2} />
        {t("backToList")}
      </button>

      <h1 className="mb-1 text-xl font-semibold text-text dark:text-text-dark">{thread.subject}</h1>
      <p className="mb-6 text-sm text-text-soft dark:text-text-soft-dark">{thread.childName ?? t("generalThread")}</p>

      <div className="mb-6 space-y-4">
        {thread.messages.length === 0 && <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("noMessages")}</p>}
        {thread.messages.map((message) => (
          <div key={message.id} className="rounded-lg bg-surface-soft p-4 dark:bg-surface-soft-dark">
            <div className="mb-1 flex items-baseline justify-between gap-2">
              <span className="text-sm font-medium text-text dark:text-text-dark">{message.senderName}</span>
              <span className="text-xs text-text-soft dark:text-text-soft-dark" style={{ fontVariantNumeric: "tabular-nums" }}>
                {new Date(message.sentAt).toLocaleString()}
              </span>
            </div>
            <p className="whitespace-pre-wrap text-sm text-text dark:text-text-dark">{message.body}</p>
          </div>
        ))}
      </div>

      <div className="flex gap-2">
        <textarea
          value={reply}
          onChange={(e) => setReply(e.target.value)}
          placeholder={t("replyPlaceholder")}
          rows={3}
          className="flex-1 rounded-lg bg-surface-soft px-3 py-2 text-sm text-text placeholder:text-placeholder focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark dark:placeholder:text-placeholder-dark"
        />
        <Button onClick={sendReply} disabled={sending || !reply.trim()}>
          {sending ? t("sending") : t("send")}
        </Button>
      </div>
    </div>
  );
}
