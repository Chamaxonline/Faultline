"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { assignIssue, type User } from "@/lib/api";
import { getClientToken } from "@/lib/session";

export function AssigneeSelect({
  issueId,
  currentAssigneeId,
  users,
}: {
  issueId: string;
  currentAssigneeId: string | null;
  users: User[];
}) {
  const router = useRouter();
  const [pending, setPending] = useState(false);

  async function handleChange(userId: string) {
    const token = getClientToken();
    if (!token) return;

    setPending(true);
    try {
      await assignIssue(issueId, userId || null, token);
      router.refresh();
    } finally {
      setPending(false);
    }
  }

  return (
    <select
      value={currentAssigneeId ?? ""}
      disabled={pending}
      onChange={(e) => handleChange(e.target.value)}
      className="rounded border border-zinc-300 bg-transparent px-2 py-1.5 text-sm dark:border-zinc-700"
    >
      <option value="">Unassigned</option>
      {users.map((u) => (
        <option key={u.id} value={u.id}>
          {u.name}
        </option>
      ))}
    </select>
  );
}
