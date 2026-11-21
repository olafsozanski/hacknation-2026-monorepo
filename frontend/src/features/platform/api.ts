import { queryOptions, useQuery } from '@tanstack/react-query'
import { api } from '@/api/client'
import type { Department, SystemInfo, UserSummary } from '@/api/types'

export const systemInfoQuery = queryOptions({
  queryKey: ['system-info'],
  queryFn: () => api<SystemInfo>('/api/system/info'),
  staleTime: 5 * 60_000,
})

export const usersQuery = queryOptions({
  queryKey: ['users', 'active'],
  queryFn: () => api<UserSummary[]>('/api/users'),
  staleTime: 5 * 60_000,
})

export const departmentsQuery = queryOptions({
  queryKey: ['departments'],
  queryFn: () => api<Department[]>('/api/departments'),
  staleTime: 5 * 60_000,
})

export const useSystemInfo = () => useQuery(systemInfoQuery)
export const useUsers = () => useQuery(usersQuery)
export const useDepartments = () => useQuery(departmentsQuery)
