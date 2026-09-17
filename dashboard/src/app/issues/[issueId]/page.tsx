import Link from "next/link";
import { cookies } from "next/headers";
import { getIssue } from "@/lib/api";
import { SESSION_COOKIE_NAME } from "@/lib/session";
import { parseEventPayload } from "@/lib/eventPayload";
import { IssueActions } from "./IssueActions";
import { EventTabs } from "./EventTabs";

export default async function IssueDetail({
  params,
}: {
  params: Promise<{ issueId: string }>;
}) {
  const { issueId } = await params;
  const token = (await cookies()).get(SESSION_COOKIE_NAME)?.value;
  const issue = await getIssue(issueId, token);

  return (
    <main className="mx-auto max-w-4xl px-6 py-12">
      <Link href="/" className="text-sm text-zinc-500 hover:underline">
        ← Projects
      </Link>

      <div className="mt-2 flex items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold text-zinc-900 dark:text-zinc-50">{issue.title}</h1>
          <p className="mt-1 text-xs text-zinc-500">
            {issue.status} · seen {issue.count}x · first {new Date(issue.firstSeen).toLocaleString()} · last{" "}
            {new Date(issue.lastSeen).toLocaleString()}
          </p>
        </div>
        <IssueActions issueId={issue.id} currentStatus={issue.status} />
      </div>

      <h2 className="mt-8 text-sm font-medium text-zinc-700 dark:text-zinc-300">Recent events</h2>
      <ul className="mt-3 space-y-4">
        {issue.recentEvents.map((event) => {
          const payload = parseEventPayload(event.rawPayload);

          return (
            <li key={event.id} className="rounded border border-zinc-200 p-3 dark:border-zinc-800">
              <p className="text-xs text-zinc-500">
                {new Date(event.timestamp).toLocaleString()} · {event.environment ?? "unknown env"}{" "}
                {event.release ? `· ${event.release}` : ""}
              </p>

              <EventTabs payload={payload} rawPayload={event.rawPayload} />
            </li>
          );
        })}
      </ul>
    </main>
  );
}
