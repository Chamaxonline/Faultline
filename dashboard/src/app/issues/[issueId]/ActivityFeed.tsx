"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { createIssueComment, type IssueComment } from "@/lib/api";
import { getClientToken } from "@/lib/session";

export function ActivityFeed({ issueId, comments }: { issueId: string; comments: IssueComment[] }) {
  const router = useRouter();
  const [text, setText] = useState("");
  const [pending, setPending] = useState(false);

  async function submit() {
    const token = getClientToken();
    if (!token || !text.trim()) return;

    setPending(true);
    try {
      await createIssueComment(issueId, text.trim(), token);
      setText("");
      router.refresh();
    } finally {
      setPending(false);
    }
  }

  return (
    <div>
      <ul className="space-y-3">
        {comments.map((c) => (
          <li key={c.id} className="text-sm">
            {c.isSystem ? (
              <p className="text-xs text-zinc-500">
                {c.body} · {new Date(c.createdAt).toLocaleString()}
              </p>
            ) : (
              <div className="rounded border border-zinc-200 p-2.5 dark:border-zinc-800">
                <p className="text-xs font-medium text-zinc-700 dark:text-zinc-300">
                  {c.authorName ?? "Deleted user"}{" "}
                  <span className="font-normal text-zinc-400">{new Date(c.createdAt).toLocaleString()}</span>
                </p>
                <p className="mt-1 whitespace-pre-wrap text-zinc-900 dark:text-zinc-100">{c.body}</p>
              </div>
            )}
          </li>
        ))}
        {comments.length === 0 && <p className="text-sm text-zinc-500">No activity yet.</p>}
      </ul>

      <div className="mt-3 flex gap-2">
        <textarea
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Leave a comment…"
          rows={2}
          className="flex-1 rounded border border-zinc-300 bg-transparent px-3 py-1.5 text-sm dark:border-zinc-700"
        />
        <button
          disabled={pending || !text.trim()}
          onClick={submit}
          className="self-end rounded bg-zinc-900 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-40 dark:bg-zinc-50 dark:text-zinc-900"
        >
          Comment
        </button>
      </div>
    </div>
  );
}
