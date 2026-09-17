"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { deleteUser, ApiError, type User } from "@/lib/api";
import { getClientToken, decodeToken } from "@/lib/session";

export function UsersTable({ users }: { users: User[] }) {
  const router = useRouter();
  const [pendingId, setPendingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const currentUserId = (() => {
    const token = getClientToken();
    return token ? decodeToken(token)?.sub : undefined;
  })();

  async function handleDelete(id: string) {
    const token = getClientToken();
    if (!token) return;

    setPendingId(id);
    setError(null);
    try {
      await deleteUser(id, token);
      router.refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not delete user");
    } finally {
      setPendingId(null);
    }
  }

  return (
    <div className="mt-6">
      {error && <p className="mb-3 text-sm text-red-500">{error}</p>}
      <ul className="divide-y divide-zinc-200 dark:divide-zinc-800">
        {users.map((u) => (
          <li key={u.id} className="flex items-center justify-between py-3">
            <div>
              <p className="text-sm font-medium text-zinc-900 dark:text-zinc-50">{u.name}</p>
              <p className="text-xs text-zinc-500">
                {u.email} · {u.role}
              </p>
            </div>
            {u.id !== currentUserId && (
              <button
                disabled={pendingId === u.id}
                onClick={() => handleDelete(u.id)}
                className="text-sm text-red-500 hover:underline"
              >
                {pendingId === u.id ? "Removing…" : "Remove"}
              </button>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
