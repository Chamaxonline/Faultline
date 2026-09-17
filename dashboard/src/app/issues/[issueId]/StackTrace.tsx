"use client";

import { useState } from "react";
import type { StackFrame } from "@/lib/eventPayload";

function FrameView({ frame }: { frame: StackFrame }) {
  return (
    <li className={`rounded border p-3 ${frame.InApp ? "border-zinc-300 dark:border-zinc-700" : "border-zinc-200 dark:border-zinc-800"}`}>
      <div className="flex items-center gap-2">
        {frame.InApp && (
          <span className="rounded bg-zinc-900 px-1.5 py-0.5 text-[10px] font-medium uppercase text-white dark:bg-zinc-50 dark:text-zinc-900">
            In App
          </span>
        )}
        <span className="truncate font-mono text-sm text-zinc-900 dark:text-zinc-50">{frame.Function ?? "<unknown>"}</span>
      </div>
      {frame.File && (
        <p className="mt-1 truncate text-xs text-zinc-500">
          {frame.File}
          {frame.Line ? `:${frame.Line}` : ""}
        </p>
      )}
      {frame.ContextLines && frame.ContextStartLine && (
        <pre className="mt-2 overflow-x-auto rounded bg-zinc-100 p-2 text-xs dark:bg-zinc-900">
          {frame.ContextLines.map((line, i) => {
            const lineNumber = frame.ContextStartLine! + i;
            const isFailingLine = lineNumber === frame.Line;
            return (
              <div
                key={lineNumber}
                className={isFailingLine ? "bg-red-500/10 font-medium text-red-600 dark:text-red-400" : "text-zinc-600 dark:text-zinc-400"}
              >
                <span className="mr-3 inline-block w-8 select-none text-right text-zinc-400">{lineNumber}</span>
                {line}
              </div>
            );
          })}
        </pre>
      )}
    </li>
  );
}

export function StackTrace({ frames }: { frames: StackFrame[] }) {
  const [showLibraryFrames, setShowLibraryFrames] = useState(false);

  const inAppFrames = frames.filter((f) => f.InApp);
  const libraryFrames = frames.filter((f) => !f.InApp);
  const hasLibraryFrames = libraryFrames.length > 0;

  // if nothing is marked in-app (e.g. no InAppAssemblyPrefixes configured), just show everything
  const visibleFrames = inAppFrames.length > 0 ? inAppFrames : frames;
  const hiddenCount = inAppFrames.length > 0 ? libraryFrames.length : 0;

  return (
    <div>
      <ul className="space-y-2">
        {visibleFrames.map((frame, i) => (
          <FrameView key={i} frame={frame} />
        ))}
      </ul>

      {hiddenCount > 0 && (
        <button
          onClick={() => setShowLibraryFrames((v) => !v)}
          className="mt-2 text-xs text-zinc-500 hover:underline"
        >
          {showLibraryFrames ? "Hide" : "Show"} {hiddenCount} library frame{hiddenCount === 1 ? "" : "s"}
        </button>
      )}

      {showLibraryFrames && hasLibraryFrames && (
        <ul className="mt-2 space-y-2">
          {libraryFrames.map((frame, i) => (
            <FrameView key={i} frame={frame} />
          ))}
        </ul>
      )}
    </div>
  );
}
