import Link from "next/link";
import { cookies } from "next/headers";
import { listIssues } from "@/lib/api";
import { SESSION_COOKIE_NAME, decodeToken } from "@/lib/session";

const levelColor: Record<string, string> = {
  fatal: "bg-red-600",
  error: "bg-red-400",
  warning: "bg-amber-400",
  info: "bg-blue-400",
};

const STATUSES = ["", "Unresolved", "Resolved", "Ignored"];
const SORTS: Record<string, string> = { lastSeen: "Last seen", firstSeen: "First seen", count: "Event count" };

export default async function ProjectIssues({
  params,
  searchParams,
}: {
  params: Promise<{ projectId: string }>;
  searchParams: Promise<{ [key: string]: string | undefined }>;
}) {
  const { projectId } = await params;
  const sp = await searchParams;

  const page = Number(sp.page ?? "1") || 1;
  const pageSize = 25;
  const token = (await cookies()).get(SESSION_COOKIE_NAME)?.value;
  const caller = token ? decodeToken(token) : null;
  const assignedToMe = sp.assignedTo === "me";

  const filters = {
    status: sp.status ?? "",
    q: sp.q ?? "",
    environment: sp.environment ?? "",
    release: sp.release ?? "",
    sort: sp.sort ?? "lastSeen",
    assignedTo: sp.assignedTo ?? "",
  };

  const apiFilters = { ...filters, assignedTo: assignedToMe ? caller?.sub : filters.assignedTo || undefined };
  const result = await listIssues(projectId, { ...apiFilters, page, pageSize }, token).catch(() => ({
    items: [],
    total: 0,
    page: 1,
    pageSize,
  }));

  const totalPages = Math.max(1, Math.ceil(result.total / pageSize));

  function buildQuery(overrides: Record<string, string | number>) {
    const merged = { ...filters, page: String(page), ...overrides };
    const qs = new URLSearchParams();
    for (const [key, value] of Object.entries(merged)) {
      if (value !== "" && value !== undefined) qs.set(key, String(value));
    }
    return `?${qs.toString()}`;
  }

  return (
    <main className="mx-auto max-w-4xl px-6 py-12">
      <Link href="/" className="text-sm text-zinc-500 hover:underline">
        ← Projects
      </Link>
      <h1 className="mt-2 text-2xl font-semibold text-zinc-900 dark:text-zinc-50">Issues</h1>

      <form className="mt-6 flex flex-wrap gap-2" action={`/projects/${projectId}`} method="get">
        <input
          type="text"
          name="q"
          defaultValue={filters.q}
          placeholder="Search title…"
          className="min-w-40 flex-1 rounded border border-zinc-300 bg-transparent px-3 py-1.5 text-sm dark:border-zinc-700"
        />
        <select name="status" defaultValue={filters.status} className="rounded border border-zinc-300 bg-transparent px-2 py-1.5 text-sm dark:border-zinc-700">
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {s || "All statuses"}
            </option>
          ))}
        </select>
        <input
          type="text"
          name="environment"
          defaultValue={filters.environment}
          placeholder="Environment"
          className="w-32 rounded border border-zinc-300 bg-transparent px-3 py-1.5 text-sm dark:border-zinc-700"
        />
        <input
          type="text"
          name="release"
          defaultValue={filters.release}
          placeholder="Release"
          className="w-32 rounded border border-zinc-300 bg-transparent px-3 py-1.5 text-sm dark:border-zinc-700"
        />
        <select name="sort" defaultValue={filters.sort} className="rounded border border-zinc-300 bg-transparent px-2 py-1.5 text-sm dark:border-zinc-700">
          {Object.entries(SORTS).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
        {caller && (
          <label className="flex items-center gap-1.5 rounded border border-zinc-300 px-3 py-1.5 text-sm dark:border-zinc-700">
            <input type="checkbox" name="assignedTo" value="me" defaultChecked={assignedToMe} />
            Assigned to me
          </label>
        )}
        <button type="submit" className="rounded bg-zinc-900 px-3 py-1.5 text-sm font-medium text-white dark:bg-zinc-50 dark:text-zinc-900">
          Apply
        </button>
      </form>

      {result.items.length === 0 && <p className="mt-8 text-sm text-zinc-500">No issues match these filters.</p>}

      <ul className="mt-6 divide-y divide-zinc-200 dark:divide-zinc-800">
        {result.items.map((issue) => (
          <li key={issue.id} className="flex items-start gap-3 py-4">
            <span className={`mt-1.5 h-2 w-2 shrink-0 rounded-full ${levelColor[issue.level] ?? "bg-zinc-400"}`} />
            <div className="min-w-0 flex-1">
              <Link href={`/issues/${issue.id}`} className="block truncate text-sm font-medium text-zinc-900 hover:underline dark:text-zinc-50">
                {issue.title}
              </Link>
              <p className="mt-1 text-xs text-zinc-500">
                {issue.status} · seen {issue.count}x · last {new Date(issue.lastSeen).toLocaleString()}
                {issue.environment ? ` · ${issue.environment}` : ""}
                {issue.release ? ` · ${issue.release}` : ""}
              </p>
            </div>
          </li>
        ))}
      </ul>

      {result.total > pageSize && (
        <div className="mt-6 flex items-center justify-between text-sm text-zinc-500">
          <span>
            Page {page} of {totalPages} · {result.total} issues
          </span>
          <div className="flex gap-2">
            {page > 1 && (
              <Link href={buildQuery({ page: page - 1 })} className="hover:underline">
                ← Previous
              </Link>
            )}
            {page < totalPages && (
              <Link href={buildQuery({ page: page + 1 })} className="hover:underline">
                Next →
              </Link>
            )}
          </div>
        </div>
      )}
    </main>
  );
}
