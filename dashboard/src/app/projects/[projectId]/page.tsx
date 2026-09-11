import Link from "next/link";
import { listIssues } from "@/lib/api";

const levelColor: Record<string, string> = {
  fatal: "bg-red-600",
  error: "bg-red-400",
  warning: "bg-amber-400",
  info: "bg-blue-400",
};

export default async function ProjectIssues({
  params,
}: {
  params: Promise<{ projectId: string }>;
}) {
  const { projectId } = await params;
  const issues = await listIssues(projectId).catch(() => []);

  return (
    <main className="mx-auto max-w-4xl px-6 py-12">
      <Link href="/" className="text-sm text-zinc-500 hover:underline">
        ← Projects
      </Link>
      <h1 className="mt-2 text-2xl font-semibold text-zinc-900 dark:text-zinc-50">Issues</h1>

      {issues.length === 0 && <p className="mt-8 text-sm text-zinc-500">No issues reported yet.</p>}

      <ul className="mt-6 divide-y divide-zinc-200 dark:divide-zinc-800">
        {issues.map((issue) => (
          <li key={issue.id} className="flex items-start gap-3 py-4">
            <span className={`mt-1.5 h-2 w-2 shrink-0 rounded-full ${levelColor[issue.level] ?? "bg-zinc-400"}`} />
            <div className="min-w-0 flex-1">
              <Link href={`/issues/${issue.id}`} className="block truncate text-sm font-medium text-zinc-900 hover:underline dark:text-zinc-50">
                {issue.title}
              </Link>
              <p className="mt-1 text-xs text-zinc-500">
                {issue.status} · seen {issue.count}x · last {new Date(issue.lastSeen).toLocaleString()}
              </p>
            </div>
          </li>
        ))}
      </ul>
    </main>
  );
}
