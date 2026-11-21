import { createBrowserRouter, redirect, type LoaderFunctionArgs } from 'react-router'
import { onUnauthorized } from '@/api/client'
import { AppShell } from '@/components/layout/AppShell'
import { RouteError } from '@/components/common/RouteError'
import { sessionQuery } from '@/features/auth/session'
import { queryClient } from './queryClient'

async function requireSession({ request }: LoaderFunctionArgs) {
  const user = await queryClient.ensureQueryData(sessionQuery)
  if (!user) {
    const url = new URL(request.url)
    const target = url.pathname + url.search
    throw redirect(target === '/' ? '/logowanie' : `/logowanie?wroc=${encodeURIComponent(target)}`)
  }
  return null
}

async function redirectIfSignedIn() {
  const user = await queryClient.ensureQueryData(sessionQuery)
  if (user) throw redirect('/')
  return null
}

async function requireAdmin() {
  const user = await queryClient.ensureQueryData(sessionQuery)
  if (!user?.isAdmin) throw redirect('/')
  return null
}

export const router = createBrowserRouter([
  {
    path: '/logowanie',
    loader: redirectIfSignedIn,
    errorElement: <RouteError />,
    lazy: () => import('@/features/auth/LoginPage').then((m) => ({ Component: m.LoginPage })),
  },
  {
    path: '/',
    loader: requireSession,
    element: <AppShell />,
    errorElement: <RouteError />,
    children: [
      {
        errorElement: <RouteError />,
        children: [
          {
            index: true,
            lazy: () => import('@/features/register/RegisterPage').then((m) => ({ Component: m.RegisterPage })),
          },
          {
            path: 'wnioski/nowy',
            lazy: () => import('@/features/intake/NewRequestPage').then((m) => ({ Component: m.NewRequestPage })),
          },
          {
            path: 'wnioski/:id',
            lazy: () => import('@/features/case/CasePage').then((m) => ({ Component: m.CasePage })),
          },
          {
            path: 'wnioski/:id/odpowiedz',
            lazy: () => import('@/features/drafting/DraftEditorPage').then((m) => ({ Component: m.DraftEditorPage })),
          },
          {
            path: 'anonimizacja',
            lazy: () => import('@/features/anonymization/AnonymizationPage').then((m) => ({ Component: m.AnonymizationPage })),
          },
          {
            path: 'archiwum',
            lazy: () => import('@/features/archive/ArchivePage').then((m) => ({ Component: m.ArchivePage })),
          },
          {
            path: 'baza-wiedzy',
            lazy: () => import('@/features/knowledge/KnowledgePage').then((m) => ({ Component: m.KnowledgePage })),
          },
          {
            path: 'wydzialy',
            lazy: () => import('@/features/departments/DepartmentsPage').then((m) => ({ Component: m.DepartmentsPage })),
          },
          {
            path: 'administracja/uzytkownicy',
            loader: requireAdmin,
            lazy: () => import('@/features/admin/UsersPage').then((m) => ({ Component: m.UsersPage })),
          },
          {
            path: 'administracja/dziennik',
            loader: requireAdmin,
            lazy: () => import('@/features/admin/AuditPage').then((m) => ({ Component: m.AuditPage })),
          },
          {
            path: 'asystent',
            lazy: () => import('@/features/assistant/AssistantPage').then((m) => ({ Component: m.AssistantPage })),
          },
          {
            path: 'dostepnosc',
            lazy: () =>
              import('@/features/accessibility/AccessibilityPage').then((m) => ({ Component: m.AccessibilityPage })),
          },
          {
            path: '*',
            lazy: () => import('@/components/common/NotFoundPage').then((m) => ({ Component: m.NotFoundPage })),
          },
        ],
      },
    ],
  },
])

onUnauthorized(() => {
  queryClient.setQueryData(sessionQuery.queryKey, null)
  if (!window.location.pathname.startsWith('/logowanie')) void router.navigate('/logowanie')
})
