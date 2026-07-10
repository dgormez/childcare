"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Megaphone, Plus } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../components/ui/table";
import { Button } from "../../../components/ui/button";
import { Input } from "../../../components/ui/input";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "../../../components/ui/dialog";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import type { AnnouncementResponse, GroupResponse, LocationResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

export default function AnnouncementsPage() {
  const t = useTranslations("announcements");
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [groups, setGroups] = useState<GroupResponse[]>([]);
  const [announcements, setAnnouncements] = useState<AnnouncementResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [composeOpen, setComposeOpen] = useState(false);
  const [locationId, setLocationId] = useState("");
  const [groupId, setGroupId] = useState("");
  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [sending, setSending] = useState(false);
  const [notice, setNotice] = useState("");

  useEffect(() => {
    (apiClient.GET as any)("/api/locations").then((result: { response: Response; data?: LocationResponse[] }) => {
      if (!result.response.ok || !result.data) return;
      setLocations(result.data);
      if (result.data.length > 0) setLocationId((current) => current || result.data![0].id);
    });
  }, []);

  useEffect(() => {
    if (!locationId) return;
    (apiClient.GET as any)("/api/groups", { params: { query: { locationId } } }).then(
      (result: { response: Response; data?: GroupResponse[] }) => {
        if (result.response.ok && result.data) setGroups(result.data);
      },
    );
  }, [locationId]);

  const load = useCallback(async () => {
    setState("loading");
    const result = await (apiClient.GET as any)("/api/announcements");
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setAnnouncements((result.data ?? []) as AnnouncementResponse[]);
    setState("loaded");
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  function locationName(id: string): string {
    return locations.find((l) => l.id === id)?.name ?? "—";
  }

  function groupName(id: string | null): string {
    if (!id) return t("groupAllOption");
    return groups.find((g) => g.id === id)?.name ?? "—";
  }

  async function send() {
    if (!locationId || !subject.trim() || !body.trim()) return;
    setSending(true);
    const result = await (apiClient.POST as any)("/api/announcements", {
      body: { locationId, groupId: groupId || null, subject: subject.trim(), body: body.trim() },
    });
    setSending(false);
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    setComposeOpen(false);
    setSubject("");
    setBody("");
    setGroupId("");
    setNotice("");
    await load();
  }

  return (
    <div>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <Button onClick={() => setComposeOpen(true)}>
          <Plus className="h-4 w-4" strokeWidth={2} />
          {t("compose")}
        </Button>
      </div>

      {notice && (
        <div className="mb-4 rounded-lg bg-surface-soft p-3 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark">
          {notice}
        </div>
      )}

      {state === "loading" && <div className="h-64 rounded-lg bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && announcements.length === 0 && <EmptyState icon={Megaphone} message={t("emptyState")} />}
      {state === "loaded" && announcements.length > 0 && (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("columnSubject")}</TableHead>
              <TableHead>{t("columnScope")}</TableHead>
              <TableHead>{t("columnSentAt")}</TableHead>
              <TableHead className="text-right">{t("columnRecipients")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {announcements.map((a) => (
              <TableRow key={a.id}>
                <TableCell className="font-medium">{a.subject}</TableCell>
                <TableCell className="text-text-soft dark:text-text-soft-dark">
                  {locationName(a.locationId)} — {groupName(a.groupId)}
                </TableCell>
                <TableCell className="text-text-soft dark:text-text-soft-dark" style={{ fontVariantNumeric: "tabular-nums" }}>
                  {new Date(a.sentAt).toLocaleString()}
                </TableCell>
                <TableCell className="text-right text-text-soft dark:text-text-soft-dark" style={{ fontVariantNumeric: "tabular-nums" }}>
                  {a.recipientCount}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <Dialog open={composeOpen} onOpenChange={setComposeOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t("compose")}</DialogTitle>
            <DialogDescription>{t("title")}</DialogDescription>
          </DialogHeader>

          <div className="space-y-3">
            <div>
              <label htmlFor="announcement-location" className="mb-1 block text-sm font-medium text-text dark:text-text-dark">{t("locationLabel")}</label>
              <select
                id="announcement-location"
                value={locationId}
                onChange={(e) => { setLocationId(e.target.value); setGroupId(""); }}
                className="h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
              >
                {locations.map((l) => (
                  <option key={l.id} value={l.id}>{l.name}</option>
                ))}
              </select>
            </div>
            <div>
              <label htmlFor="announcement-group" className="mb-1 block text-sm font-medium text-text dark:text-text-dark">{t("groupLabel")}</label>
              <select
                id="announcement-group"
                value={groupId}
                onChange={(e) => setGroupId(e.target.value)}
                className="h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
              >
                <option value="">{t("groupAllOption")}</option>
                {groups.map((g) => (
                  <option key={g.id} value={g.id}>{g.name}</option>
                ))}
              </select>
            </div>
            <div>
              <label htmlFor="announcement-subject" className="mb-1 block text-sm font-medium text-text dark:text-text-dark">{t("subjectLabel")}</label>
              <Input id="announcement-subject" value={subject} onChange={(e) => setSubject(e.target.value)} />
            </div>
            <div>
              <label htmlFor="announcement-body" className="mb-1 block text-sm font-medium text-text dark:text-text-dark">{t("bodyLabel")}</label>
              <textarea
                id="announcement-body"
                value={body}
                onChange={(e) => setBody(e.target.value)}
                rows={4}
                className="w-full rounded-lg bg-surface-soft px-3 py-2 text-sm text-text placeholder:text-placeholder focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark dark:placeholder:text-placeholder-dark"
              />
            </div>
            <Button onClick={send} disabled={sending || !subject.trim() || !body.trim()} className="w-full">
              {sending ? t("sending") : t("send")}
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
