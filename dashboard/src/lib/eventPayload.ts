// Shape of Event.RawPayload's JSON — mirrors Faultline.Contracts.ErrorEvent on the
// Api side, serialized with default System.Text.Json casing (PascalCase, not camelCase).

export type StackFrame = {
  Function: string | null;
  File: string | null;
  Line: number | null;
  InApp: boolean;
  ContextLines: string[] | null;
  ContextStartLine: number | null;
};

export type Breadcrumb = {
  Timestamp: string;
  Message: string;
  Category: string;
  Level: string;
};

export type EventPayload = {
  ExceptionType: string;
  Message: string;
  StackTrace: string | null;
  Frames: StackFrame[];
  Level: string;
  Release: string | null;
  Environment: string | null;
  UserContext: string | null;
  Tags: Record<string, string>;
  Extra: Record<string, string>;
  Timestamp: string;
  Breadcrumbs: Breadcrumb[];
};

export function parseEventPayload(rawPayload: string): EventPayload | null {
  try {
    return JSON.parse(rawPayload) as EventPayload;
  } catch {
    return null;
  }
}
