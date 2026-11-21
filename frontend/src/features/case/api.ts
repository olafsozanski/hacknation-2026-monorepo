import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/api/client'
import type {
  DepartmentQuery,
  DepartmentSuggestion,
  DocumentInfo,
  DocumentKind,
  ExtensionResult,
  HistoryItem,
  RequestDepartment,
  RequestDetail,
  RequestStatus,
  SearchHit,
  UpdateRequestBody,
} from '@/api/types'

export const caseKeys = {
  detail: (id: string) => ['request', id] as const,
  history: (id: string) => ['request', id, 'history'] as const,
  similar: (id: string) => ['request', id, 'similar'] as const,
  suggestions: (id: string) => ['request', id, 'department-suggestions'] as const,
  query: (id: string) => ['request', id, 'department-query'] as const,
  documentText: (documentId: string) => ['document', documentId, 'text'] as const,
}

export const requestDetailQuery = (id: string) =>
  queryOptions({ queryKey: caseKeys.detail(id), queryFn: () => api<RequestDetail>(`/api/requests/${id}`) })

export const historyQuery = (id: string) =>
  queryOptions({ queryKey: caseKeys.history(id), queryFn: () => api<HistoryItem[]>(`/api/requests/${id}/history`) })

export const similarQuery = (id: string) =>
  queryOptions({
    queryKey: caseKeys.similar(id),
    queryFn: () => api<SearchHit[]>(`/api/requests/${id}/similar`),
    staleTime: 5 * 60_000,
  })

export const departmentSuggestionsQuery = (id: string) =>
  queryOptions({
    queryKey: caseKeys.suggestions(id),
    queryFn: () => api<DepartmentSuggestion[]>(`/api/requests/${id}/department-suggestions`),
    staleTime: 5 * 60_000,
  })

export const departmentQueryQuery = (id: string) =>
  queryOptions({
    queryKey: caseKeys.query(id),
    queryFn: () => api<DepartmentQuery>(`/api/requests/${id}/department-query`),
    staleTime: 0,
  })

export const documentTextQuery = (documentId: string) =>
  queryOptions({
    queryKey: caseKeys.documentText(documentId),
    queryFn: () => api<{ text: string }>(`/api/documents/${documentId}/text`),
    staleTime: Infinity,
  })

function useCaseMutation<TVariables, TResult>(id: string, mutationFn: (variables: TVariables) => Promise<TResult>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['request', id] })
      void queryClient.invalidateQueries({ queryKey: ['requests'] })
    },
  })
}

export const useUpdateRequest = (id: string) =>
  useCaseMutation(id, (body: UpdateRequestBody) => api<void>(`/api/requests/${id}`, { method: 'PUT', body }))

export const useChangeStatus = (id: string) =>
  useCaseMutation(id, (body: { status: RequestStatus; respondedOn?: string | null; version: number }) =>
    api<RequestDetail>(`/api/requests/${id}/status`, { method: 'POST', body }),
  )

export const useExtendDeadline = (id: string) =>
  useCaseMutation(id, (body: { days: number; reason: string; version: number; kind: 'Query' | 'Clarification' }) =>
    api<ExtensionResult>(`/api/requests/${id}/extension`, { method: 'POST', body }),
  )

export const useSetDepartments = (id: string) =>
  useCaseMutation(id, (departmentIds: string[]) =>
    api<RequestDepartment[]>(`/api/requests/${id}/departments`, { method: 'PUT', body: { departmentIds } }),
  )

export const useUpdateRequestDepartment = (id: string) =>
  useCaseMutation(id, ({ departmentId, askedOn, answeredOn }: { departmentId: string; askedOn: string | null; answeredOn: string | null }) =>
    api<RequestDepartment>(`/api/requests/${id}/departments/${departmentId}`, { method: 'PATCH', body: { askedOn, answeredOn } }),
  )

export const useUploadDocument = (id: string) =>
  useCaseMutation(id, ({ file, kind, departmentId }: { file: File; kind: DocumentKind; departmentId?: string | null }) => {
    const form = new FormData()
    form.append('file', file)
    form.append('kind', kind)
    if (departmentId) form.append('departmentId', departmentId)
    return api<DocumentInfo>(`/api/requests/${id}/documents`, { method: 'POST', body: form })
  })

export const useDeleteDocument = (id: string) =>
  useCaseMutation(id, (documentId: string) => api<void>(`/api/documents/${documentId}`, { method: 'DELETE' }))
