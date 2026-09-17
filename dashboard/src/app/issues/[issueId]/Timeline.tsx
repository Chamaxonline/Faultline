import type { TimelinePoint } from "@/lib/api";

export function Timeline({ points }: { points: TimelinePoint[] }) {
  const max = Math.max(1, ...points.map((p) => p.count));

  return (
    <div>
      <div className="flex h-16 items-end gap-1">
        {points.map((p) => {
          const heightPct = p.count === 0 ? 2 : Math.max(8, Math.round((p.count / max) * 100));
          const label = new Date(p.date).toLocaleDateString(undefined, { month: "short", day: "numeric" });
          return (
            <div key={p.date} className="group relative flex-1" title={`${label}: ${p.count} event${p.count === 1 ? "" : "s"}`}>
              <div
                className={`w-full rounded-t ${p.count > 0 ? "bg-zinc-900 dark:bg-zinc-50" : "bg-zinc-200 dark:bg-zinc-800"}`}
                style={{ height: `${heightPct}%` }}
              />
            </div>
          );
        })}
      </div>
      <div className="mt-1 flex justify-between text-[10px] text-zinc-400">
        <span>{new Date(points[0].date).toLocaleDateString(undefined, { month: "short", day: "numeric" })}</span>
        <span>{new Date(points[points.length - 1].date).toLocaleDateString(undefined, { month: "short", day: "numeric" })}</span>
      </div>
    </div>
  );
}
