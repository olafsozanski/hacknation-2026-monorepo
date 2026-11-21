export type IsoDate = string

export type RequestStatus = 'NoAction' | 'QueriesSent' | 'Extended' | 'ResponsePrepared' | 'ResponseSent'
export type Urgency = 'Overdue' | 'DueToday' | 'DueTomorrow' | 'DueSoon' | 'OnTrack' | 'Closed'
export type ReplyChannel = 'Email' | 'Post' | 'Epuap' | 'InPerson' | 'Other'
export type DepartmentKind = 'Department' | 'Office'
export type DocumentKind = 'Request' | 'DepartmentReply' | 'Attachment'
export type KnowledgeKind = 'Law' | 'Ordinance' | 'ArchivedResponse' | 'Guideline' | 'Other'
export type RedactionCategory =
  | 'PersonName'
  | 'Address'
  | 'Pesel'
  | 'IdDocument'
  | 'Phone'
  | 'Email'
  | 'BankAccount'
  | 'Nip'
  | 'DateOfBirth'
  | 'Other'
export type RedactionStatus = 'Suggested' | 'Approved' | 'Rejected'
export type RedactionSource = 'Rule' | 'Ai' | 'Manual'
export type SearchHitKind = 'Request' | 'Knowledge'
export type SuggestionSource = 'Ai' | 'History' | 'Keywords'

export type UserSummary = { id: string; fullName: string; initials: string }

export type SessionUser = { id: string; userName: string; fullName: string; initials: string; isAdmin: boolean }
export type SessionResponse = { user: SessionUser | null; csrfToken: string }

export type SystemInfo = {
  organizationName: string
  unitName: string
  ai: { enabled: boolean; isLocal: boolean; provider: string; chatModel: string }
}

export type RequestListItem = {
  id: string
  sequenceNumber: number
  caseNumber: string
  subject: string
  requesterName: string | null
  receivedOn: IsoDate
  dueOn: IsoDate
  effectiveDueOn: IsoDate
  extensionDays: number | null
  respondedOn: IsoDate | null
  status: RequestStatus
  urgency: Urgency
  daysLeft: number
  assignee: UserSummary | null
  departmentSymbols: string[]
}

export type RegisterSummary = {
  overdue: number
  dueToday: number
  dueTomorrow: number
  dueSoon: number
  open: number
  closed: number
  total: number
}

export type DeadlinePreview = { dueOn: IsoDate; latestExtendedDueOn: IsoDate }

export type RequestDepartment = {
  departmentId: string
  symbol: string
  name: string
  email: string | null
  askedOn: IsoDate | null
  answeredOn: IsoDate | null
}

export type DocumentInfo = {
  id: string
  kind: DocumentKind
  fileName: string
  sizeBytes: number
  departmentId: string | null
  departmentSymbol: string | null
  uploadedAt: string
  uploadedBy: string
  hasText: boolean
}

export type DraftSummary = {
  updatedAt: string
  generatedByAi: boolean
  suggestedRedactions: number
  approvedRedactions: number
}

export type RequestDetail = {
  id: string
  year: number
  sequenceNumber: number
  caseNumber: string
  subject: string
  content: string
  requesterName: string | null
  requesterContact: string | null
  replyChannel: ReplyChannel
  receivedOn: IsoDate
  dueOn: IsoDate
  effectiveDueOn: IsoDate
  extendedDueOn: IsoDate | null
  extensionDays: number | null
  extensionReason: string | null
  latestExtendedDueOn: IsoDate
  respondedOn: IsoDate | null
  status: RequestStatus
  urgency: Urgency
  daysLeft: number
  assignee: UserSummary | null
  departments: RequestDepartment[]
  documents: DocumentInfo[]
  draft: DraftSummary | null
  version: number
}

export type CreateRequestBody = {
  subject: string
  content?: string | null
  requesterName?: string | null
  requesterContact?: string | null
  replyChannel: ReplyChannel
  receivedOn: IsoDate
  dueOn?: IsoDate | null
  caseNumber?: string | null
  assigneeId?: string | null
  sourceFileId?: string | null
  departmentIds?: string[]
}

export type UpdateRequestBody = {
  subject: string
  content: string | null
  requesterName: string | null
  requesterContact: string | null
  replyChannel: ReplyChannel
  receivedOn: IsoDate
  dueOn: IsoDate
  caseNumber: string
  assigneeId: string | null
  version: number
}

export type ExtensionResult = { extendedDueOn: IsoDate; noticeText: string }
export type DepartmentQuery = { subject: string; body: string; replyBy: IsoDate }
export type HistoryItem = { at: string; userName: string; action: string; details: string | null }

export type Department = {
  id: string
  symbol: string
  name: string
  kind: DepartmentKind
  scope: string
  email: string | null
  isActive: boolean
}

export type SearchHit = {
  kind: SearchHitKind
  id: string
  title: string
  caseNumber: string | null
  date: IsoDate | null
  snippet: string
  score: number
  status: RequestStatus | null
  departmentSymbols: string[]
  knowledgeKind: KnowledgeKind | null
}

export type DepartmentSuggestion = {
  departmentId: string
  symbol: string
  name: string
  reason: string
  confidence: number
  similarCaseCount: number
  averageResponseDays: number | null
  source: SuggestionSource
}

export type IntakeSuggestion = {
  subject: string | null
  requesterName: string | null
  requesterContact: string | null
  replyChannel: ReplyChannel | null
  requestDate: IsoDate | null
  requestedItems: string[]
  looksLikePublicInformationRequest: boolean
  remarks: string | null
}

export type IntakeResult = {
  fileId: string | null
  fileName: string | null
  text: string
  suggestion: IntakeSuggestion
  suggestedCaseNumber: string
  suggestedReceivedOn: IsoDate
  suggestedDueOn: IsoDate
  similarCases: SearchHit[]
  departmentSuggestions: DepartmentSuggestion[]
  warnings: string[]
  aiUsed: boolean
}

export type Redaction = {
  start: number
  length: number
  text: string
  category: RedactionCategory
  status: RedactionStatus
  source: RedactionSource
}

export type Draft = {
  content: string
  redactions: Redaction[]
  generatedByAi: boolean
  updatedAt: string
  updatedBy: string
  version: number
}

export type GenerateDraftResult = { draft: Draft; warnings: string[]; aiUsed: boolean }
export type DetectRedactionsResult = { redactions: Redaction[]; warnings: string[]; aiUsed: boolean }

export type KnowledgeDocument = {
  id: string
  title: string
  kind: KnowledgeKind
  fileName: string | null
  chunkCount: number
  indexed: boolean
  createdAt: string
  createdBy: string
}

export type UserListItem = {
  id: string
  userName: string
  fullName: string
  initials: string
  isAdmin: boolean
  isActive: boolean
  isLockedOut: boolean
}
