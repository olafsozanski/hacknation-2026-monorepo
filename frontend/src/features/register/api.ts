import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api, query } from '@/api/client'
import type { RegisterSummary, RequestListItem, RequestStatus } from '@/api/types'

export type RegisterFilters = {
  year: number
  status?: RequestStatus
  assigneeId?: string
  search?: string
  dueOn?: string
}

export const requestListQuery = (filters: RegisterFilters) =>
  queryOptions({
    queryKey: ['requests', 'list', filters],
    queryFn: ({ signal }) => api<RequestListItem[]>(`/api/requests${query(filters)}`, { signal }),
    placeholderData: keepPreviousData,
  })

export const registerSummaryQuery = (year: number) =>
  queryOptions({
    queryKey: ['requests', 'summary', year],
    queryFn: () => api<RegisterSummary>(`/api/requests/summary${query({ year })}`),
  })

export const registerYearsQuery = queryOptions({
  queryKey: ['requests', 'years'],
  queryFn: () => api<number[]>('/api/requests/years'),
  staleTime: 10 * 60_000,
})
