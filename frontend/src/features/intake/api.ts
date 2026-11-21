import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, query } from '@/api/client'
import type { CreateRequestBody, DeadlinePreview, IntakeResult } from '@/api/types'

export function useAnalyzeFile() {
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData()
      form.append('file', file)
      return api<IntakeResult>('/api/intake', { method: 'POST', body: form })
    },
  })
}

export function useAnalyzeText() {
  return useMutation({
    mutationFn: (text: string) => api<IntakeResult>('/api/intake/text', { method: 'POST', body: { text } }),
  })
}

export const deadlinePreviewQuery = (receivedOn: string) =>
  queryOptions({
    queryKey: ['deadline-preview', receivedOn],
    queryFn: () => api<DeadlinePreview>(`/api/requests/deadline-preview${query({ receivedOn })}`),
    enabled: /^\d{4}-\d{2}-\d{2}$/.test(receivedOn),
    staleTime: Infinity,
  })

export function useCreateRequest() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateRequestBody) => api<{ id: string }>('/api/requests', { method: 'POST', body }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['requests'] }),
  })
}
