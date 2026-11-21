import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query'
import { ApiError, api, downloadFile } from '@/api/client'
import type { DetectRedactionsResult, Draft, GenerateDraftResult, Redaction } from '@/api/types'

export const draftKey = (requestId: string) => ['request', requestId, 'draft'] as const

export const draftQuery = (requestId: string) =>
  queryOptions({
    queryKey: draftKey(requestId),
    queryFn: async () => {
      try {
        return await api<Draft>(`/api/requests/${requestId}/draft`)
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null
        throw error
      }
    },
  })

export function useGenerateDraft(requestId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api<GenerateDraftResult>(`/api/requests/${requestId}/draft/generate`, { method: 'POST' }),
    onSuccess: (result) => {
      queryClient.setQueryData(draftKey(requestId), result.draft)
      void queryClient.invalidateQueries({ queryKey: ['request', requestId], exact: true })
      void queryClient.invalidateQueries({ queryKey: ['request', requestId, 'history'] })
    },
  })
}

export function useSaveDraft(requestId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: { content: string; redactions: Redaction[]; version: number | null }) =>
      api<Draft>(`/api/requests/${requestId}/draft`, { method: 'PUT', body }),
    onSuccess: (draft) => {
      queryClient.setQueryData(draftKey(requestId), draft)
      void queryClient.invalidateQueries({ queryKey: ['request', requestId], exact: true })
      void queryClient.invalidateQueries({ queryKey: ['request', requestId, 'history'] })
    },
  })
}

export function useDetectRedactions(requestId: string) {
  return useMutation({
    mutationFn: (body: { content: string; existing: Redaction[] }) =>
      api<DetectRedactionsResult>(`/api/requests/${requestId}/draft/detect`, { method: 'POST', body }),
  })
}

export function exportDraft(requestId: string, caseNumber: string, format: 'docx' | 'txt') {
  const fallback = `${caseNumber.replace(/[^\p{L}\p{N}]+/gu, '-')}-odpowiedz.${format}`
  return downloadFile(`/api/requests/${requestId}/draft/export?format=${format}`, fallback)
}
