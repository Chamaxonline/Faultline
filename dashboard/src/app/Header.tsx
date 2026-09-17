"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { clearClientToken } from "@/lib/session";

export function Header({ name, role }: { name: string; role: string }) {
  const router = useRouter();

  function handleLogout() {
    clearClientToken();
    router.push("/login");
    router.refresh();
  }

  return (
    <header className="flex items-center justify-between border-b border-zinc-200 px-6 py-3 text-sm dark:border-zinc-800">
      <nav className="flex items-center gap-4">
        <Link href="/" className="font-medium text-zinc-900 dark:text-zinc-50">
          Faultline
        </Link>
        {role === "Admin" && (
          <Link href="/users" className="text-zinc-500 hover:underline">
            Users
          </Link>
        )}
      </nav>
      <div className="flex items-center gap-3 text-zinc-500">
        <span>
          {name} · {role}
        </span>
        <button onClick={handleLogout} className="hover:underline">
          Sign out
        </button>
      </div>
    </header>
  );
}
