import { queryOptions, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, setCsrfToken } from '@/api/client'
import type { SessionResponse, SessionUser } from '@/api/types'

export const sessionQuery = queryOptions({
  queryKey: ['session'],
  queryFn: async () => {
    const session = await api<SessionResponse>('/api/auth/session')
    setCsrfToken(session.csrfToken)
    return session.user
  },
  staleTime: Infinity,
})

export function useCurrentUser(): SessionUser {
  const { data } = useQuery(sessionQuery)
  if (!data) throw new Error('useCurrentUser must be used inside the authenticated part of the app.')
  return data
}

export function useLogin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (credentials: { userName: string; password: string }) => {
      await queryClient.ensureQueryData(sessionQuery)
      return api<SessionResponse>('/api/auth/login', { method: 'POST', body: credentials })
    },
    onSuccess: (session) => {
      setCsrfToken(session.csrfToken)
      queryClient.clear()
      queryClient.setQueryData(sessionQuery.queryKey, session.user)
    },
  })
}

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api<SessionResponse>('/api/auth/logout', { method: 'POST' }),
    onSuccess: (session) => {
      setCsrfToken(session.csrfToken)
      queryClient.clear()
      queryClient.setQueryData(sessionQuery.queryKey, null)
    },
  })
}

export function useChangePassword() {
  return useMutation({
    mutationFn: (body: { currentPassword: string; newPassword: string }) =>
      api<void>('/api/auth/change-password', { method: 'POST', body }),
  })
}
