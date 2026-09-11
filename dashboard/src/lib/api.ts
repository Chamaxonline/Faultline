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
  recentEvents: EventItem[];
};

async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, { ...init, cache: "no-store" });
  if (!res.ok) throw new Error(`${init?.method ?? "GET"} ${path} failed: ${res.status}`);
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

export const listProjects = () => apiFetch<Project[]>("/api/v1/projects");

export const listIssues = (projectId: string, params: IssueListParams = {}) => {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "") search.set(key, String(value));
  }
  const qs = search.toString();
  return apiFetch<PagedResult<IssueSummary>>(`/api/v1/projects/${projectId}/issues${qs ? `?${qs}` : ""}`);
};

export const getIssue = (issueId: string) => apiFetch<IssueDetail>(`/api/v1/issues/${issueId}`);

export const updateIssueStatus = (issueId: string, status: string) =>
  apiFetch<void>(`/api/v1/issues/${issueId}/status`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ status }),
  });
