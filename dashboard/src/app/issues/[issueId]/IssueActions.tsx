"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { updateIssueStatus } from "@/lib/api";
import { getClientToken } from "@/lib/session";

export function IssueActions({ issueId, currentStatus }: { issueId: string; currentStatus: string }) {
  const router = useRouter();
  const [pending, setPending] = useState(false);

  async function setStatus(status: string) {
    const token = getClientToken();
    if (!token) return;

    setPending(true);
    try {
      await updateIssueStatus(issueId, status, token);
      router.refresh();
    } finally {
      setPending(false);
    }
  }

  const buttonClass = (status: string) =>
    `rounded px-3 py-1.5 text-sm font-medium ${
      currentStatus.toLowerCase() === status
        ? "bg-zinc-900 text-white dark:bg-zinc-50 dark:text-zinc-900"
        : "bg-zinc-100 text-zinc-700 hover:bg-zinc-200 dark:bg-zinc-800 dark:text-zinc-300"
    }`;

  return (
    <div className="flex gap-2">
      <button disabled={pending} onClick={() => setStatus("resolved")} className={buttonClass("resolved")}>
        Resolve
      </button>
      <button disabled={pending} onClick={() => setStatus("ignored")} className={buttonClass("ignored")}>
        Ignore
      </button>
      <button disabled={pending} onClick={() => setStatus("unresolved")} className={buttonClass("unresolved")}>
        Unresolve
      </button>
    </div>
  );
}
