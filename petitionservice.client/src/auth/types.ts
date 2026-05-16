export interface PetitionListItem {
  id: number;
  title: string;
  category?: string;
  createdAt: string;
  author: string;
  signatures: number;
}

export interface Petition extends PetitionListItem {
  content: string;
}

export interface PetitionListResponse {
  items: PetitionListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  sortBy: string;
  sortDir: string;
}

export interface PetitionAttachment {
  id: number;
  petitionId: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedBy: string;
  uploadedAt: string;
}

export interface PreSignedDownloadResponse {
  url: string;
  expiresAtUtc: string;
}

export interface PetitionAiAssistResponse {
  title: string;
  content: string;
  category?: string;
  summary: string;
  provider: string;
  model: string;
}

export interface AuthResponse {
  username: string;
  roles: string[];
  accessTokenExpiresAtUtc: string;
}

export interface AuthSession {
  username: string;
  roles: string[];
}
