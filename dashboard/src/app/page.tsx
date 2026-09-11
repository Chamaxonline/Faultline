import Link from "next/link";
import { listProjects } from "@/lib/api";
import { NewProjectForm } from "./NewProjectForm";

export default async function Home() {
  const projects = await listProjects().catch(() => []);

  return (
    <main className="mx-auto max-w-3xl px-6 py-12">
      <h1 className="text-2xl font-semibold text-zinc-900 dark:text-zinc-50">Faultline</h1>
      <p className="mt-1 text-sm text-zinc-500">Projects</p>

      <NewProjectForm />

      {projects.length === 0 && (
        <p className="mt-8 text-sm text-zinc-500">
          No projects yet, or the API is unreachable at {process.env.NEXT_PUBLIC_API_URL}.
        </p>
      )}

      <ul className="mt-6 divide-y divide-zinc-200 dark:divide-zinc-800">
        {projects.map((p) => (
          <li key={p.id} className="py-4">
            <Link href={`/projects/${p.id}`} className="text-base font-medium text-zinc-900 hover:underline dark:text-zinc-50">
              {p.name}
            </Link>
            <p className="text-xs text-zinc-500">key: {p.publicKey}</p>
          </li>
        ))}
      </ul>
    </main>
  );
}
