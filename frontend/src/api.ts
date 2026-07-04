export type SessionStatus = "Scanning" | "Running" | "Reviewing" | "Completed" | "Failed";
export type FileChangeStatus = "Pending" | "Modernizing" | "Ready" | "Unchanged" | "Failed";
export type HunkDecision = "Pending" | "Accepted" | "Rejected";

export interface AiModelInfo {
  id: string;
  displayName: string;
}

export interface AiProviderInfo {
  id: string;
  displayName: string;
  models: AiModelInfo[];
}

export interface SkillInfo {
  id: string;
  displayName: string;
  language: string;
  targetVersion: string;
  fileExtensions: string[];
}

export interface Config {
  skills: SkillInfo[];
  providers: AiProviderInfo[];
  hasApiKey: boolean;
}

export interface DirectoryEntry {
  name: string;
  path: string;
}

export interface BrowseResult {
  path: string;
  parent: string | null;
  directories: DirectoryEntry[];
}

export interface FileSummary {
  id: string;
  relativePath: string;
  status: FileChangeStatus;
  hunkCount: number;
  acceptedCount: number;
  rejectedCount: number;
  applied: boolean;
  error: string | null;
}

export interface ReviewResult {
  verdict: string;
  summary: string;
}

export interface Session {
  id: string;
  status: SessionStatus;
  projectPath: string;
  skillId: string;
  providerId: string;
  agentModelId: string;
  reviewModelId: string;
  review: ReviewResult | null;
  error: string | null;
  files: FileSummary[];
}

export interface Hunk {
  id: number;
  decision: HunkDecision;
  originalStart: number;
  originalLines: string[];
  modernizedLines: string[];
}

export interface FileDetail {
  id: string;
  relativePath: string;
  status: FileChangeStatus;
  error: string | null;
  originalContent: string;
  hunks: Hunk[];
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    headers: { "Content-Type": "application/json" },
    ...init,
  });
  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`;
    try {
      const body = await response.json();
      if (body?.error) message = body.error;
    } catch {
      /* not JSON */
    }
    throw new Error(message);
  }
  if (response.status === 202 || response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const api = {
  getConfig: () => request<Config>("/api/config"),

  browse: (path?: string) =>
    request<BrowseResult>(`/api/browse${path ? `?path=${encodeURIComponent(path)}` : ""}`),

  startSession: (body: {
    projectPath: string;
    skillId: string;
    providerId: string;
    agentModelId: string;
    reviewModelId: string;
  }) => request<Session>("/api/sessions", { method: "POST", body: JSON.stringify(body) }),

  getSession: (sessionId: string) => request<Session>(`/api/sessions/${sessionId}`),

  getFile: (sessionId: string, fileId: string) =>
    request<FileDetail>(`/api/sessions/${sessionId}/files/${fileId}`),

  setHunkDecision: (sessionId: string, fileId: string, hunkId: number, decision: HunkDecision) =>
    request<FileDetail>(`/api/sessions/${sessionId}/files/${fileId}/hunks/${hunkId}`, {
      method: "POST",
      body: JSON.stringify({ decision }),
    }),

  acceptFile: (sessionId: string, fileId: string) =>
    request<FileDetail>(`/api/sessions/${sessionId}/files/${fileId}/accept`, { method: "POST" }),

  revertFile: (sessionId: string, fileId: string) =>
    request<FileDetail>(`/api/sessions/${sessionId}/files/${fileId}/revert`, { method: "POST" }),

  adjustFile: (sessionId: string, fileId: string, instructions: string) =>
    request<void>(`/api/sessions/${sessionId}/files/${fileId}/adjust`, {
      method: "POST",
      body: JSON.stringify({ instructions }),
    }),

  startReview: (sessionId: string) =>
    request<void>(`/api/sessions/${sessionId}/review`, { method: "POST" }),

  apply: (sessionId: string) =>
    request<{ written: string[] }>(`/api/sessions/${sessionId}/apply`, { method: "POST" }),
};
