"use client";
import { useEffect, useId, useState } from "react";
import { useTranslations } from "next-intl";
import { Paperclip } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { Button } from "../../../components/ui/button";
import { Input } from "../../../components/ui/input";
import type { GroupResponse, LocationResponse } from "../../../lib/types";

const ALLOWED_CONTENT_TYPES = ["application/pdf", "image/jpeg", "image/png"];
const MAX_SIZE_BYTES = 10 * 1024 * 1024; // FR-003/FR-017, matches contracts/email-communications-api.md
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Comma-separated free text -> a trimmed, deduplicated list of addresses; empty entries
 * (trailing commas, blank input) are dropped rather than treated as invalid. */
function parseEmailList(raw: string): string[] {
  const seen = new Set<string>();
  for (const entry of raw.split(",")) {
    const trimmed = entry.trim();
    if (trimmed) seen.add(trimmed);
  }
  return [...seen];
}

interface Attachment {
  objectPath: string;
  fileName: string;
  contentType: string;
}

interface SendResult {
  sentCount: number;
  skippedNoEmailCount: number;
  providerFailureCount: number;
}

export default function CommunicationsPage() {
  const t = useTranslations("communications");
  const attachmentInputId = useId();

  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [groups, setGroups] = useState<GroupResponse[]>([]);
  const [locationId, setLocationId] = useState("");
  const [groupId, setGroupId] = useState("");
  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [cc, setCc] = useState("");
  const [bcc, setBcc] = useState("");
  const [attachment, setAttachment] = useState<Attachment | null>(null);
  const [attachmentStatus, setAttachmentStatus] = useState<"idle" | "uploading" | "error">("idle");
  const [attachmentError, setAttachmentError] = useState("");
  const [recipientCount, setRecipientCount] = useState<number | null>(null);
  const [sending, setSending] = useState(false);
  const [notice, setNotice] = useState("");
  const [result, setResult] = useState<SendResult | null>(null);

  useEffect(() => {
    (apiClient.GET as any)("/api/locations").then((r: { response: Response; data?: LocationResponse[] }) => {
      if (!r.response.ok || !r.data) return;
      setLocations(r.data);
      if (r.data.length > 0) setLocationId((current) => current || r.data![0].id);
    });
  }, []);

  useEffect(() => {
    if (!locationId) return;
    (apiClient.GET as any)("/api/groups", { params: { query: { locationId } } }).then(
      (r: { response: Response; data?: GroupResponse[] }) => {
        if (r.response.ok && r.data) setGroups(r.data);
      },
    );
  }, [locationId]);

  // FR-016: recipient-count preview, so a zero-recipient scope is visible before send is attempted.
  useEffect(() => {
    if (!locationId) return;
    setRecipientCount(null);
    (apiClient.GET as any)("/api/email/bulk-send/recipient-count", {
      params: { query: groupId ? { locationId, groupId } : { locationId } },
    }).then((r: { response: Response; data?: { recipientCount: number } }) => {
      if (r.response.ok && r.data) setRecipientCount(r.data.recipientCount);
    });
  }, [locationId, groupId]);

  async function handleAttachmentChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;

    if (!ALLOWED_CONTENT_TYPES.includes(file.type)) {
      setAttachmentStatus("error");
      setAttachmentError(t("invalidContentType"));
      return;
    }
    if (file.size > MAX_SIZE_BYTES) {
      setAttachmentStatus("error");
      setAttachmentError(t("attachmentTooLarge"));
      return;
    }

    setAttachmentStatus("uploading");
    const urlResult = await (apiClient.POST as any)("/api/email/attachments/upload-url", {
      body: { contentType: file.type },
    });
    if (!urlResult.response.ok) {
      setAttachmentStatus("error");
      setAttachmentError(t("genericError"));
      return;
    }

    const { uploadUrl, objectPath } = urlResult.data as { uploadUrl: string; objectPath: string };
    const putResult = await fetch(uploadUrl, { method: "PUT", body: file, headers: { "Content-Type": file.type } });
    if (!putResult.ok) {
      setAttachmentStatus("error");
      setAttachmentError(t("genericError"));
      return;
    }

    setAttachment({ objectPath, fileName: file.name, contentType: file.type });
    setAttachmentStatus("idle");
  }

  async function send() {
    if (!locationId || !subject.trim() || !body.trim()) return;

    const ccList = parseEmailList(cc);
    const bccList = parseEmailList(bcc);
    if (ccList.some((address) => !EMAIL_PATTERN.test(address))) {
      setNotice(t("ccInvalid"));
      return;
    }
    if (bccList.some((address) => !EMAIL_PATTERN.test(address))) {
      setNotice(t("bccInvalid"));
      return;
    }

    setSending(true);
    setNotice("");
    setResult(null);

    const sendResult = await (apiClient.POST as any)("/api/email/bulk-send", {
      body: {
        locationId,
        groupId: groupId || null,
        subject: subject.trim(),
        body: body.trim(),
        attachmentObjectPath: attachment?.objectPath ?? null,
        attachmentFileName: attachment?.fileName ?? null,
        attachmentContentType: attachment?.contentType ?? null,
        cc: ccList,
        bcc: bccList,
      },
    });
    setSending(false);

    if (!sendResult.response.ok) {
      setNotice(t("genericError"));
      return;
    }

    setResult(sendResult.data as SendResult);
    setSubject("");
    setBody("");
    setCc("");
    setBcc("");
    setAttachment(null);
  }

  const zeroRecipients = recipientCount === 0;

  return (
    <div className="mx-auto max-w-lg">
      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>

      {notice && (
        <div className="mb-4 rounded-lg bg-surface-soft p-3 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark">
          {notice}
        </div>
      )}

      {result && (
        <div className="mb-4 space-y-1 rounded-lg bg-surface-soft p-3 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark">
          <p>{t("resultSent", { count: result.sentCount })}</p>
          {result.skippedNoEmailCount > 0 && <p className="text-text-soft dark:text-text-soft-dark">{t("resultSkipped", { count: result.skippedNoEmailCount })}</p>}
          {result.providerFailureCount > 0 && <p className="text-danger dark:text-danger-dark">{t("resultFailed", { count: result.providerFailureCount })}</p>}
        </div>
      )}

      <div className="space-y-3">
        <div>
          <label htmlFor="comm-location" className="mb-1 block text-sm font-medium text-text dark:text-text-dark">{t("locationLabel")}</label>
          <select
            id="comm-location"
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
          <label htmlFor="comm-group" className="mb-1 block text-sm font-medium text-text dark:text-text-dark">{t("groupLabel")}</label>
          <select
            id="comm-group"
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

        {recipientCount !== null && (
          <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("recipientCount", { count: recipientCount })}</p>
        )}

        <div>
          <label htmlFor="comm-subject" className="mb-1 block text-sm font-medium text-text dark:text-text-dark">{t("subjectLabel")}</label>
          <Input id="comm-subject" value={subject} onChange={(e) => setSubject(e.target.value)} />
        </div>
        <div>
          <label htmlFor="comm-body" className="mb-1 block text-sm font-medium text-text dark:text-text-dark">{t("bodyLabel")}</label>
          <textarea
            id="comm-body"
            value={body}
            onChange={(e) => setBody(e.target.value)}
            rows={6}
            className="w-full rounded-lg bg-surface-soft px-3 py-2 text-sm text-text placeholder:text-placeholder focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark dark:placeholder:text-placeholder-dark"
          />
        </div>

        <div>
          <label htmlFor="comm-cc" className="mb-1 block text-sm font-medium text-text dark:text-text-dark">{t("ccLabel")}</label>
          <Input id="comm-cc" value={cc} onChange={(e) => setCc(e.target.value)} placeholder="director2@example.com, ..." />
        </div>
        <div>
          <label htmlFor="comm-bcc" className="mb-1 block text-sm font-medium text-text dark:text-text-dark">{t("bccLabel")}</label>
          <Input id="comm-bcc" value={bcc} onChange={(e) => setBcc(e.target.value)} placeholder="archive@example.com, ..." />
          <p className="mt-1 text-xs text-text-soft dark:text-text-soft-dark">{t("ccBccHint")}</p>
        </div>

        <div>
          <p className="mb-1 block text-sm font-medium text-text dark:text-text-dark">{t("attachmentLabel")}</p>
          <div className="flex items-center gap-2">
            {attachment && (
              <span className="inline-flex items-center gap-1 text-sm text-text-soft dark:text-text-soft-dark">
                <Paperclip className="h-3 w-3" strokeWidth={2} />
                {attachment.fileName}
              </span>
            )}
            <label htmlFor={attachmentInputId} className="cursor-pointer text-sm text-primary-hover hover:underline dark:text-primary-hover-dark">
              {attachmentStatus === "uploading" ? t("attachmentUploading") : attachment ? t("attachmentRemove") : t("attachmentUpload")}
            </label>
            {attachment && (
              <button
                type="button"
                onClick={() => setAttachment(null)}
                className="text-sm text-primary-hover hover:underline dark:text-primary-hover-dark"
              >
                {t("attachmentRemove")}
              </button>
            )}
            <input
              id={attachmentInputId}
              type="file"
              accept="application/pdf,image/jpeg,image/png"
              className="sr-only"
              disabled={attachmentStatus === "uploading"}
              onChange={handleAttachmentChange}
            />
          </div>
          <span role="status" aria-live="polite" className="sr-only">
            {attachmentStatus === "uploading" ? t("attachmentUploading") : ""}
          </span>
          {attachmentStatus === "error" && <p className="mt-1 text-xs text-danger dark:text-danger-dark">{attachmentError}</p>}
        </div>

        <Button
          onClick={send}
          disabled={sending || !subject.trim() || !body.trim() || zeroRecipients || attachmentStatus === "uploading"}
          className="w-full"
        >
          {sending ? t("sending") : t("send")}
        </Button>
      </div>
    </div>
  );
}
