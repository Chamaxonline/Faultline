const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5065";

export type Project = {
  id: string;
  name: string;
  slug: string;
  publicKey: string;
};

export type IssueSummary = {
  id: string;
  title: string;
  level: string;
  status: string;
  count: number;
  firstSeen: string;
  lastSeen: string;
  environment: string | null;
  release: string | null;
  assignedToUserId: string | null;
};

export type PagedResult<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export type IssueListParams = {
  status?: string;
  q?: string;
  environment?: string;
  release?: string;
  sort?: string;
  assignedTo?: string;
  page?: number;
  pageSize?: number;
};

export type EventItem = {
  id: string;
  timestamp: string;
  release: string | null;
  environment: string | null;
  rawPayload: string;
};

export type IssueDetail = IssueSummary & {
  exceptionType: string | null;
  ignoreUntilCount: number | null;
  ignoreUntilDate: string | null;
  recentEvents: EventItem[];
};

export type User = {
  id: string;
  email: string;
  name: string;
  role: "Admin" | "Member";
};

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

async function apiFetch<T>(path: string, token: string | undefined, init?: RequestInit): Promise<T> {
  const headers: Record<string, string> = { ...(init?.headers as Record<string, string>) };
  if (token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(`${API_URL}${path}`, { ...init, headers, cache: "no-store" });
  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new ApiError(res.status, body?.error ?? `${init?.method ?? "GET"} ${path} failed: ${res.status}`);
  }
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

export const login = (email: string, password: string) =>
  apiFetch<{ token: string; user: User }>("/api/v1/auth/login", undefined, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });

export const listProjects = (token: string) => apiFetch<Project[]>("/api/v1/projects", token);

export const createProject = (name: string, token: string) =>
  apiFetch<Project>("/api/v1/projects", token, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ name }),
  });

export const listIssues = (projectId: string, params: IssueListParams = {}, token?: string) => {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "") search.set(key, String(value));
  }
  const qs = search.toString();
  return apiFetch<PagedResult<IssueSummary>>(`/api/v1/projects/${projectId}/issues${qs ? `?${qs}` : ""}`, token);
};

export const getIssue = (issueId: string, token?: string) =>
  apiFetch<IssueDetail>(`/api/v1/issues/${issueId}`, token);

/** page 1 = latest event, higher page numbers go further back in time */
export const getIssueEvent = (issueId: string, page: number, token?: string) =>
  apiFetch<PagedResult<EventItem>>(`/api/v1/issues/${issueId}/events?page=${page}`, token);

export type TimelinePoint = { date: string; count: number };

export const getIssueTimeline = (issueId: string, token?: string) =>
  apiFetch<TimelinePoint[]>(`/api/v1/issues/${issueId}/timeline`, token);

export type TagDistribution = { sampledEvents: number; tags: Record<string, Record<string, number>> };

export const getIssueTagDistribution = (issueId: string, token?: string) =>
  apiFetch<TagDistribution>(`/api/v1/issues/${issueId}/tags`, token);

export const updateIssueStatus = (
  issueId: string,
  status: string,
  token: string,
  ignoreCondition?: { ignoreUntilCount?: number; ignoreUntilDate?: string },
) =>
  apiFetch<void>(`/api/v1/issues/${issueId}/status`, token, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ status, ...ignoreCondition }),
  });

export const assignIssue = (issueId: string, userId: string | null, token: string) =>
  apiFetch<void>(`/api/v1/issues/${issueId}/assign`, token, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userId }),
  });

export type IssueComment = {
  id: string;
  authorUserId: string | null;
  authorName: string | null;
  body: string;
  isSystem: boolean;
  createdAt: string;
};

export const listIssueComments = (issueId: string, token?: string) =>
  apiFetch<IssueComment[]>(`/api/v1/issues/${issueId}/comments`, token);

export const createIssueComment = (issueId: string, text: string, token: string) =>
  apiFetch<IssueComment>(`/api/v1/issues/${issueId}/comments`, token, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ text }),
  });

export const listUsers = (token: string) => apiFetch<User[]>("/api/v1/users", token);

export const createUser = (body: { email: string; name: string; password: string; role?: string }, token: string) =>
  apiFetch<User>("/api/v1/users", token, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

export const deleteUser = (userId: string, token: string) =>
  apiFetch<void>(`/api/v1/users/${userId}`, token, { method: "DELETE" });
