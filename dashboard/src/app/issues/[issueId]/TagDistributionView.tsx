import type { TagDistribution } from "@/lib/api";

export function TagDistributionView({ distribution }: { distribution: TagDistribution }) {
  const entries = Object.entries(distribution.tags);
  if (entries.length === 0) {
    return <p className="text-xs text-zinc-500">No tags recorded on recent events.</p>;
  }

  return (
    <div className="space-y-3">
      {entries.map(([key, values]) => {
        const sortedValues = Object.entries(values).sort((a, b) => b[1] - a[1]);
        return (
          <div key={key}>
            <p className="mb-1 text-xs font-medium text-zinc-700 dark:text-zinc-300">{key}</p>
            <div className="space-y-1">
              {sortedValues.slice(0, 5).map(([value, count]) => {
                const pct = Math.round((count / distribution.sampledEvents) * 100);
                return (
                  <div key={value} className="relative overflow-hidden rounded bg-zinc-100 dark:bg-zinc-800">
                    <div className="h-5 bg-zinc-300 dark:bg-zinc-700" style={{ width: `${pct}%` }} />
                    <div className="absolute inset-0 flex items-center justify-between px-2 text-[11px]">
                      <span className="truncate text-zinc-700 dark:text-zinc-300">{value}</span>
                      <span className="text-zinc-500">{pct}%</span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        );
      })}
      <p className="text-[10px] text-zinc-400">Based on the last {distribution.sampledEvents} event(s).</p>
    </div>
  );
}
