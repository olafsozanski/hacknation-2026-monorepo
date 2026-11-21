import { Avatar, Badge, Box, Grid, Stack } from '@chakra-ui/react'
import { useQuery } from '@tanstack/react-query'
import { useParams } from 'react-router'
import { DeadlineBadge } from '@/components/common/DeadlineBadge'
import { PageHeader } from '@/components/common/PageHeader'
import { ErrorState, LoadingState } from '@/components/common/States'
import { StatusBadge } from '@/components/common/StatusBadge'
import { requestDetailQuery } from './api'
import { CaseActions } from './components/CaseActions'
import { CaseDetailsPanel } from './components/CaseDetailsPanel'
import { CaseProgress } from './components/CaseProgress'
import { DepartmentsPanel } from './components/DepartmentsPanel'
import { DocumentsPanel } from './components/DocumentsPanel'
import { HistoryPanel } from './components/HistoryPanel'
import { RequestContentPanel } from './components/RequestContentPanel'
import { ResponsePanel } from './components/ResponsePanel'
import { SimilarCasesPanel } from './components/SimilarCasesPanel'

export function CasePage() {
  const { id = '' } = useParams()
  const detail = useQuery(requestDetailQuery(id))

  if (detail.isPending) return <LoadingState label="Wczytywanie sprawy…" />
  if (detail.isError) return <ErrorState error={detail.error} onRetry={() => detail.refetch()} title="Nie udało się wczytać sprawy" />

  const request = detail.data
  return (
    <>
      <PageHeader
        eyebrow={request.caseNumber}
        title={request.subject}
        documentTitle={`${request.caseNumber} – ${request.subject}`}
        breadcrumbs={[{ label: 'Rejestr wniosków', to: '/' }, { label: request.caseNumber }]}
        badges={
          <>
            <StatusBadge status={request.status} extensionDays={request.extensionDays} />
            <DeadlineBadge urgency={request.urgency} daysLeft={request.daysLeft} />
            {request.assignee && (
              <Badge variant="outline" size="md" gap="1.5" ps="0.5">
                <Avatar.Root size="2xs" variant="solid">
                  <Avatar.Fallback aria-hidden>{request.assignee.initials}</Avatar.Fallback>
                </Avatar.Root>
                Prowadzi: {request.assignee.fullName}
              </Badge>
            )}
          </>
        }
        actions={<CaseActions request={request} />}
      />
      <Stack gap="5">
        <CaseProgress request={request} />
        <Grid templateColumns={{ base: '1fr', xl: 'minmax(0, 1fr) 380px' }} gap="5" alignItems="start">
          <Stack gap="5" minW="0">
            <RequestContentPanel request={request} />
            <DepartmentsPanel request={request} />
            <DocumentsPanel request={request} />
            <ResponsePanel request={request} />
          </Stack>
          <Box as="aside" aria-label="Informacje o sprawie">
            <Stack gap="5">
              <CaseDetailsPanel request={request} />
              <SimilarCasesPanel requestId={request.id} />
              <HistoryPanel requestId={request.id} />
            </Stack>
          </Box>
        </Grid>
      </Stack>
    </>
  )
}
