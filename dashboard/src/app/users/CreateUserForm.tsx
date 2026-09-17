"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { createUser, ApiError } from "@/lib/api";
import { getClientToken } from "@/lib/session";

export function CreateUserForm() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [name, setName] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState("Member");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const token = getClientToken();
    if (!token) return;

    setPending(true);
    setError(null);
    try {
      await createUser({ email, name, password, role }, token);
      setEmail("");
      setName("");
      setPassword("");
      setRole("Member");
      router.refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not create user");
    } finally {
      setPending(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="mt-6 flex flex-wrap items-end gap-2">
      <input
        type="text"
        value={name}
        onChange={(e) => setName(e.target.value)}
        placeholder="Name"
        required
        className="rounded border border-zinc-300 bg-transparent px-3 py-1.5 text-sm dark:border-zinc-700"
      />
      <input
        type="email"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        placeholder="Email"
        required
        className="rounded border border-zinc-300 bg-transparent px-3 py-1.5 text-sm dark:border-zinc-700"
      />
      <input
        type="password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        placeholder="Password (min 8 chars)"
        required
        minLength={8}
        className="rounded border border-zinc-300 bg-transparent px-3 py-1.5 text-sm dark:border-zinc-700"
      />
      <select
        value={role}
        onChange={(e) => setRole(e.target.value)}
        className="rounded border border-zinc-300 bg-transparent px-2 py-1.5 text-sm dark:border-zinc-700"
      >
        <option value="Member">Member</option>
        <option value="Admin">Admin</option>
      </select>
      <button
        type="submit"
        disabled={pending}
        className="rounded bg-zinc-900 px-3 py-1.5 text-sm font-medium text-white dark:bg-zinc-50 dark:text-zinc-900"
      >
        {pending ? "Creating…" : "Create user"}
      </button>
      {error && <p className="w-full text-sm text-red-500">{error}</p>}
    </form>
  );
}
