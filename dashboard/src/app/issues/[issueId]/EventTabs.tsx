"use client";

import { useState } from "react";
import type { EventPayload } from "@/lib/eventPayload";
import { StackTrace } from "./StackTrace";

const TABS = ["Stack Trace", "Tags", "Breadcrumbs", "Context", "Raw"] as const;
type Tab = (typeof TABS)[number];

const levelColor: Record<string, string> = {
  fatal: "bg-red-600",
  error: "bg-red-400",
  warning: "bg-amber-400",
  info: "bg-blue-400",
};

function KeyValueList({ entries, empty }: { entries: [string, string][]; empty: string }) {
  if (entries.length === 0) return <p className="text-xs text-zinc-500">{empty}</p>;
  return (
    <dl className="space-y-1">
      {entries.map(([key, value]) => (
        <div key={key} className="flex gap-2 text-xs">
          <dt className="w-32 shrink-0 truncate text-zinc-500">{key}</dt>
          <dd className="truncate text-zinc-900 dark:text-zinc-50">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

export function EventTabs({ payload, rawPayload }: { payload: EventPayload | null; rawPayload: string }) {
  const [tab, setTab] = useState<Tab>("Stack Trace");

  return (
    <div className="mt-3">
      <div className="flex gap-1 border-b border-zinc-200 dark:border-zinc-800">
        {TABS.map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-2 py-1.5 text-xs font-medium ${
              tab === t
                ? "border-b-2 border-zinc-900 text-zinc-900 dark:border-zinc-50 dark:text-zinc-50"
                : "text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300"
            }`}
          >
            {t}
          </button>
        ))}
      </div>

      <div className="pt-3">
        {tab === "Stack Trace" &&
          (payload && payload.Frames.length > 0 ? (
            <StackTrace frames={payload.Frames} />
          ) : (
            <p className="text-xs text-zinc-500">No stack trace captured for this event.</p>
          ))}

        {tab === "Tags" && (
          <KeyValueList entries={payload ? Object.entries(payload.Tags) : []} empty="No tags on this event." />
        )}

        {tab === "Breadcrumbs" &&
          (payload && payload.Breadcrumbs.length > 0 ? (
            <ul className="space-y-2">
              {payload.Breadcrumbs.map((crumb, i) => (
                <li key={i} className="flex items-start gap-2 text-xs">
                  <span className={`mt-1 h-1.5 w-1.5 shrink-0 rounded-full ${levelColor[crumb.Level] ?? "bg-zinc-400"}`} />
                  <div className="min-w-0 flex-1">
                    <span className="text-zinc-500">{new Date(crumb.Timestamp).toLocaleTimeString()}</span>{" "}
                    <span className="rounded bg-zinc-100 px-1 py-0.5 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400">
                      {crumb.Category}
                    </span>{" "}
                    <span className="text-zinc-900 dark:text-zinc-50">{crumb.Message}</span>
                  </div>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-xs text-zinc-500">No breadcrumbs recorded before this event.</p>
          ))}

        {tab === "Context" && (
          <div className="space-y-3">
            <div>
              <h4 className="mb-1 text-xs font-medium text-zinc-700 dark:text-zinc-300">User</h4>
              <p className="text-xs text-zinc-500">{payload?.UserContext ?? "No user context captured."}</p>
            </div>
            <div>
              <h4 className="mb-1 text-xs font-medium text-zinc-700 dark:text-zinc-300">Extra</h4>
              <KeyValueList entries={payload ? Object.entries(payload.Extra) : []} empty="No extra data on this event." />
            </div>
          </div>
        )}

        {tab === "Raw" && (
          <pre className="max-h-64 overflow-auto whitespace-pre-wrap break-words text-xs text-zinc-700 dark:text-zinc-300">
            {JSON.stringify(payload ?? JSON.parse(rawPayload), null, 2)}
          </pre>
        )}
      </div>
    </div>
  );
}
