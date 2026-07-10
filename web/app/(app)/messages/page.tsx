"use client";
import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { MessageSquare, UserPlus } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../components/ui/table";
import { Badge } from "../../../components/ui/badge";
import { Button } from "../../../components/ui/button";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import { InviteParentDialog } from "../../../components/InviteParentDialog";
import type { MessageThreadSummaryResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

export default function MessagesPage() {
  const t = useTranslations("messages");
  const [threads, setThreads] = useState<MessageThreadSummaryResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [inviteOpen, setInviteOpen] = useState(false);

  const load = useCallback(async () => {
    setState("loading");
    const result = await (apiClient.GET as any)("/api/message-threads");
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setThreads((result.data ?? []) as MessageThreadSummaryResponse[]);
    setState("loaded");
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <Button onClick={() => setInviteOpen(true)}>
          <UserPlus className="h-4 w-4" strokeWidth={2} />
          {t("invite")}
        </Button>
      </div>

      {state === "loading" && <div className="h-64 rounded-lg bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && threads.length === 0 && <EmptyState icon={MessageSquare} message={t("emptyState")} />}
      {state === "loaded" && threads.length > 0 && (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("columnFamily")}</TableHead>
              <TableHead>{t("columnSubject")}</TableHead>
              <TableHead>{t("columnLastActivity")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {threads.map((thread) => (
              <TableRow key={thread.id}>
                <TableCell className="font-medium">
                  <Link
                    href={`/messages/${thread.id}`}
                    className="text-left underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
                  >
                    {thread.childName ?? t("generalThread")}
                  </Link>
                </TableCell>
                <TableCell className="text-text-soft dark:text-text-soft-dark">
                  {thread.subject}
                  {thread.unreadFromParentCount > 0 && (
                    <Badge variant="neutral" className="ml-2">
                      {t("unread")} ({thread.unreadFromParentCount})
                    </Badge>
                  )}
                </TableCell>
                <TableCell className="text-text-soft dark:text-text-soft-dark" style={{ fontVariantNumeric: "tabular-nums" }}>
                  {new Date(thread.lastActivityAt).toLocaleString()}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <InviteParentDialog open={inviteOpen} onOpenChange={setInviteOpen} />
    </div>
  );
}
