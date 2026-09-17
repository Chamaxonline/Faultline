"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { updateIssueStatus } from "@/lib/api";
import { getClientToken } from "@/lib/session";

export function IssueActions({
  issueId,
  currentStatus,
  ignoreUntilCount,
  ignoreUntilDate,
}: {
  issueId: string;
  currentStatus: string;
  ignoreUntilCount: number | null;
  ignoreUntilDate: string | null;
}) {
  const router = useRouter();
  const [pending, setPending] = useState(false);
  const [showIgnoreForm, setShowIgnoreForm] = useState(false);
  const [untilCount, setUntilCount] = useState("");
  const [untilDate, setUntilDate] = useState("");

  async function setStatus(status: string, ignoreCondition?: { ignoreUntilCount?: number; ignoreUntilDate?: string }) {
    const token = getClientToken();
    if (!token) return;

    setPending(true);
    try {
      await updateIssueStatus(issueId, status, token, ignoreCondition);
      setShowIgnoreForm(false);
      router.refresh();
    } finally {
      setPending(false);
    }
  }

  function submitIgnoreForm() {
    setStatus("ignored", {
      ignoreUntilCount: untilCount ? Number(untilCount) : undefined,
      ignoreUntilDate: untilDate ? new Date(untilDate).toISOString() : undefined,
    });
  }

  const buttonClass = (status: string) =>
    `rounded px-3 py-1.5 text-sm font-medium ${
      currentStatus.toLowerCase() === status
        ? "bg-zinc-900 text-white dark:bg-zinc-50 dark:text-zinc-900"
        : "bg-zinc-100 text-zinc-700 hover:bg-zinc-200 dark:bg-zinc-800 dark:text-zinc-300"
    }`;

  return (
    <div>
      <div className="flex gap-2">
        <button disabled={pending} onClick={() => setStatus("resolved")} className={buttonClass("resolved")}>
          Resolve
        </button>
        <button disabled={pending} onClick={() => setShowIgnoreForm((v) => !v)} className={buttonClass("ignored")}>
          Ignore
        </button>
        <button disabled={pending} onClick={() => setStatus("unresolved")} className={buttonClass("unresolved")}>
          Unresolve
        </button>
      </div>

      {currentStatus.toLowerCase() === "ignored" && (ignoreUntilCount || ignoreUntilDate) && (
        <p className="mt-1.5 text-xs text-zinc-500">
          Ignored until{ignoreUntilCount ? ` ${ignoreUntilCount} occurrences` : ""}
          {ignoreUntilCount && ignoreUntilDate ? " or" : ""}
          {ignoreUntilDate ? ` ${new Date(ignoreUntilDate).toLocaleDateString()}` : ""}
        </p>
      )}

      {showIgnoreForm && (
        <div className="mt-2 flex flex-wrap items-center gap-2 rounded border border-zinc-200 p-2 text-sm dark:border-zinc-800">
          <label className="flex items-center gap-1.5 text-xs text-zinc-500">
            Until
            <input
              type="number"
              min={1}
              placeholder="N occurrences"
              value={untilCount}
              onChange={(e) => setUntilCount(e.target.value)}
              className="w-28 rounded border border-zinc-300 bg-transparent px-2 py-1 dark:border-zinc-700"
            />
          </label>
          <span className="text-xs text-zinc-500">or</span>
          <input
            type="date"
            value={untilDate}
            onChange={(e) => setUntilDate(e.target.value)}
            className="rounded border border-zinc-300 bg-transparent px-2 py-1 text-xs dark:border-zinc-700"
          />
          <button
            disabled={pending}
            onClick={submitIgnoreForm}
            className="rounded bg-zinc-900 px-2.5 py-1 text-xs font-medium text-white dark:bg-zinc-50 dark:text-zinc-900"
          >
            Ignore
          </button>
        </div>
      )}
    </div>
  );
}
