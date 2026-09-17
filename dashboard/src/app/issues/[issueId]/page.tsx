import Link from "next/link";
import { cookies } from "next/headers";
import { getIssue, getIssueEvent, getIssueTimeline, getIssueTagDistribution } from "@/lib/api";
import { SESSION_COOKIE_NAME } from "@/lib/session";
import { parseEventPayload } from "@/lib/eventPayload";
import { IssueActions } from "./IssueActions";
import { EventTabs } from "./EventTabs";
import { Timeline } from "./Timeline";
import { TagDistributionView } from "./TagDistributionView";

export default async function IssueDetail({
  params,
  searchParams,
}: {
  params: Promise<{ issueId: string }>;
  searchParams: Promise<{ event?: string }>;
}) {
  const { issueId } = await params;
  const sp = await searchParams;
  const eventPage = Math.max(1, Number(sp.event ?? "1") || 1);

  const token = (await cookies()).get(SESSION_COOKIE_NAME)?.value;
  const [issue, eventsResult, timeline, tagDistribution] = await Promise.all([
    getIssue(issueId, token),
    getIssueEvent(issueId, eventPage, token).catch(() => null),
    getIssueTimeline(issueId, token).catch(() => null),
    getIssueTagDistribution(issueId, token).catch(() => null),
  ]);
  const event = eventsResult?.items[0] ?? null;
  const total = eventsResult?.total ?? 0;

  const payload = event ? parseEventPayload(event.rawPayload) : null;

  function pagerLink(page: number) {
    return `/issues/${issueId}?event=${page}`;
  }

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

      {timeline && (
        <div className="mt-6">
          <Timeline points={timeline} />
        </div>
      )}

      {tagDistribution && (
        <div className="mt-6">
          <h2 className="mb-2 text-sm font-medium text-zinc-700 dark:text-zinc-300">Tag distribution</h2>
          <TagDistributionView distribution={tagDistribution} />
        </div>
      )}

      <div className="mt-8 flex items-center justify-between">
        <h2 className="text-sm font-medium text-zinc-700 dark:text-zinc-300">Event</h2>
        {total > 0 && (
          <div className="flex items-center gap-3 text-xs text-zinc-500">
            <span>
              {total - eventPage + 1} of {total}
            </span>
            <div className="flex gap-1">
              <Link
                href={pagerLink(total)}
                aria-disabled={eventPage >= total}
                className={eventPage >= total ? "pointer-events-none opacity-40" : "hover:underline"}
              >
                First
              </Link>
              <Link
                href={pagerLink(eventPage + 1)}
                aria-disabled={eventPage >= total}
                className={eventPage >= total ? "pointer-events-none opacity-40" : "hover:underline"}
              >
                ← Older
              </Link>
              <Link
                href={pagerLink(eventPage - 1)}
                aria-disabled={eventPage <= 1}
                className={eventPage <= 1 ? "pointer-events-none opacity-40" : "hover:underline"}
              >
                Newer →
              </Link>
              <Link
                href={pagerLink(1)}
                aria-disabled={eventPage <= 1}
                className={eventPage <= 1 ? "pointer-events-none opacity-40" : "hover:underline"}
              >
                Latest
              </Link>
            </div>
          </div>
        )}
      </div>

      {event ? (
        <div className="mt-3 rounded border border-zinc-200 p-3 dark:border-zinc-800">
          <p className="text-xs text-zinc-500">
            {new Date(event.timestamp).toLocaleString()} · {event.environment ?? "unknown env"}{" "}
            {event.release ? `· ${event.release}` : ""}
          </p>

          <EventTabs payload={payload} rawPayload={event.rawPayload} />
        </div>
      ) : (
        <p className="mt-3 text-sm text-zinc-500">No events captured for this issue yet.</p>
      )}
    </main>
  );
}
