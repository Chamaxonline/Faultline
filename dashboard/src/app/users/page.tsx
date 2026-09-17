import Link from "next/link";
import { cookies } from "next/headers";
import { listUsers, ApiError } from "@/lib/api";
import { SESSION_COOKIE_NAME } from "@/lib/session";
import { CreateUserForm } from "./CreateUserForm";
import { UsersTable } from "./UsersTable";

export default async function UsersPage() {
  const token = (await cookies()).get(SESSION_COOKIE_NAME)?.value;

  if (!token) {
    return (
      <main className="mx-auto max-w-2xl px-6 py-12">
        <p className="text-sm text-zinc-500">Session expired — please sign in again.</p>
      </main>
    );
  }

  try {
    const users = await listUsers(token);

    return (
      <main className="mx-auto max-w-2xl px-6 py-12">
        <Link href="/" className="text-sm text-zinc-500 hover:underline">
          ← Projects
        </Link>
        <h1 className="mt-2 text-2xl font-semibold text-zinc-900 dark:text-zinc-50">Users</h1>

        <CreateUserForm />
        <UsersTable users={users} />
      </main>
    );
  } catch (err) {
    const message =
      err instanceof ApiError && err.status === 403
        ? "Admin access required to manage users."
        : "Could not load users — check the API is running.";

    return (
      <main className="mx-auto max-w-2xl px-6 py-12">
        <p className="text-sm text-zinc-500">{message}</p>
      </main>
    );
  }
}
