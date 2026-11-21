import { Alert, Badge, Button, Flex, Stack, Text } from '@chakra-ui/react'
import { CheckCircle2, Download, FilePen, Send, Sparkles } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import type { RequestDetail } from '@/api/types'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { Panel } from '@/components/common/Panel'
import { notifyError, notifySuccess } from '@/components/ui/toaster'
import { exportDraft, useGenerateDraft } from '@/features/drafting/api'
import { formatDateTime, plural } from '@/lib/dates'
import { useChangeStatus } from '../api'

export function ResponsePanel({ request }: { request: RequestDetail }) {
  const navigate = useNavigate()
  const generate = useGenerateDraft(request.id)
  const changeStatus = useChangeStatus(request.id)
  const [confirmSent, setConfirmSent] = useState(false)
  const replies = request.documents.filter((d) => d.kind === 'DepartmentReply').length
  const draft = request.draft

  const runGenerate = () =>
    generate.mutate(undefined, {
      onSuccess: (result) => {
        for (const warning of result.warnings) notifyError(new Error(warning), 'Uwaga')
        navigate(`/wnioski/${request.id}/odpowiedz`)
      },
      onError: (e) => notifyError(e, 'Nie udało się przygotować projektu'),
    })

  const setStatus = (status: 'ResponsePrepared' | 'ResponseSent') =>
    changeStatus.mutate(
      { status, version: request.version },
      {
        onSuccess: () => {
          setConfirmSent(false)
          notifySuccess(status === 'ResponseSent' ? 'Oznaczono odpowiedź jako wysłaną' : 'Oznaczono odpowiedź jako przygotowaną')
        },
        onError: (e) => notifyError(e),
      },
    )

  return (
    <Panel
      id="odpowiedz"
      step={3}
      title="Odpowiedź dla wnioskodawcy"
      description="Projekt pisma przygotowany na podstawie odpowiedzi wydziałów, z propozycją anonimizacji."
    >
      {!draft ? (
        <Stack gap="4">
          {replies === 0 && (
            <Alert.Root status="warning" variant="subtle">
              <Alert.Indicator />
              <Alert.Description>
                Nie dodano jeszcze odpowiedzi wydziałów. Projekt będzie zawierał tylko szablon pisma z miejscami do uzupełnienia.
              </Alert.Description>
            </Alert.Root>
          )}
          <Flex gap="3" align="center" wrap="wrap">
            <Button onClick={runGenerate} loading={generate.isPending} loadingText="Przygotowuję projekt…">
              <Sparkles size={16} aria-hidden /> Przygotuj projekt odpowiedzi
            </Button>
            {replies > 0 && (
              <Text fontSize="sm" color="fg.muted">
                Na podstawie {replies} {plural(replies, 'odpowiedzi wydziału', 'odpowiedzi wydziałów', 'odpowiedzi wydziałów')}.
              </Text>
            )}
          </Flex>
        </Stack>
      ) : (
        <Stack gap="4">
          <Flex gap="2" wrap="wrap" align="center" fontSize="sm">
            <Text color="fg.muted">Ostatnia zmiana: {formatDateTime(draft.updatedAt)}</Text>
            {draft.generatedByAi && (
              <Badge colorPalette="purple" variant="subtle" gap="1">
                <Sparkles size={12} aria-hidden /> wygenerowany przez AI
              </Badge>
            )}
            {draft.suggestedRedactions > 0 ? (
              <Badge colorPalette="orange" variant="subtle">
                {draft.suggestedRedactions} {plural(draft.suggestedRedactions, 'fragment', 'fragmenty', 'fragmentów')} do weryfikacji
              </Badge>
            ) : (
              <Badge colorPalette="green" variant="subtle">
                Anonimizacja zweryfikowana
              </Badge>
            )}
            {draft.approvedRedactions > 0 && (
              <Badge variant="outline">
                {draft.approvedRedactions} {plural(draft.approvedRedactions, 'fragment ukryty', 'fragmenty ukryte', 'fragmentów ukrytych')}
              </Badge>
            )}
          </Flex>
          <Flex gap="2" wrap="wrap">
            <Button onClick={() => navigate(`/wnioski/${request.id}/odpowiedz`)}>
              <FilePen size={16} aria-hidden /> Otwórz projekt odpowiedzi
            </Button>
            <Button variant="outline" onClick={() => exportDraft(request.id, request.caseNumber, 'docx').catch((e) => notifyError(e))}>
              <Download size={16} aria-hidden /> Pobierz DOCX
            </Button>
            {request.status !== 'ResponsePrepared' && request.status !== 'ResponseSent' && (
              <Button variant="outline" onClick={() => setStatus('ResponsePrepared')} loading={changeStatus.isPending}>
                <CheckCircle2 size={16} aria-hidden /> Oznacz jako przygotowaną
              </Button>
            )}
            {request.status === 'ResponsePrepared' && (
              <Button variant="outline" colorPalette="green" onClick={() => setConfirmSent(true)}>
                <Send size={16} aria-hidden /> Oznacz jako wysłaną
              </Button>
            )}
          </Flex>
        </Stack>
      )}
      <ConfirmDialog
        open={confirmSent}
        title="Oznaczyć odpowiedź jako wysłaną?"
        confirmLabel="Tak, odpowiedź wysłana"
        loading={changeStatus.isPending}
        onCancel={() => setConfirmSent(false)}
        onConfirm={() => setStatus('ResponseSent')}
      >
        Sprawa zostanie zamknięta z dzisiejszą datą udzielenia odpowiedzi. Asystent niczego nie wysyła — potwierdź, że
        odpowiedź została przekazana wnioskodawcy.
      </ConfirmDialog>
    </Panel>
  )
}
