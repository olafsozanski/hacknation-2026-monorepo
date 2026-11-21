import { Alert, Box, Button, Field, Flex, Grid, Menu, Portal, SegmentGroup, Text, Textarea } from '@chakra-ui/react'
import { useQuery } from '@tanstack/react-query'
import { CheckCircle2, ChevronDown, Download, Eye, PenLine, RefreshCw, Save, ShieldCheck, Sparkles } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import { useBlocker, useNavigate, useParams } from 'react-router'
import type { Draft, Redaction } from '@/api/types'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { PageHeader } from '@/components/common/PageHeader'
import { EmptyHint, ErrorState, LoadingState } from '@/components/common/States'
import { notifyError, notifySuccess } from '@/components/ui/toaster'
import { requestDetailQuery, useChangeStatus } from '@/features/case/api'
import { formatTime, plural } from '@/lib/dates'
import { draftQuery, exportDraft, useDetectRedactions, useGenerateDraft, useSaveDraft } from './api'
import { RedactedDocument } from '@/components/redaction/RedactedDocument'
import { RedactionPanel } from '@/components/redaction/RedactionPanel'
import {
  addManual,
  addPhrase,
  approveAllSuggested,
  countByStatus,
  placeholderPattern,
  withStatus,
} from '@/components/redaction/redactions'

type Mode = 'review' | 'edit' | 'preview'
type ExportFormat = 'docx' | 'txt'

export function DraftEditorPage() {
  const { id = '' } = useParams()
  const request = useQuery(requestDetailQuery(id))
  const draft = useQuery(draftQuery(id))
  const generate = useGenerateDraft(id)
  const [generation, setGeneration] = useState(0)

  if (request.isPending || draft.isPending) return <LoadingState label="Wczytywanie projektu odpowiedzi…" />
  if (request.isError) return <ErrorState error={request.error} onRetry={() => request.refetch()} />
  if (draft.isError) return <ErrorState error={draft.error} onRetry={() => draft.refetch()} />

  const breadcrumbs = [
    { label: 'Rejestr wniosków', to: '/' },
    { label: request.data.caseNumber, to: `/wnioski/${id}` },
    { label: 'Projekt odpowiedzi' },
  ]

  if (!draft.data) {
    return (
      <>
        <PageHeader title="Projekt odpowiedzi" eyebrow={request.data.caseNumber} breadcrumbs={breadcrumbs} />
        <EmptyHint
          title="Nie przygotowano jeszcze projektu odpowiedzi"
          description="Asystent przygotuje projekt na podstawie odpowiedzi wydziałów dołączonych do sprawy."
          action={
            <Button onClick={() => generate.mutate(undefined, { onError: (e) => notifyError(e) })} loading={generate.isPending}>
              <Sparkles size={16} aria-hidden /> Przygotuj projekt
            </Button>
          }
        />
      </>
    )
  }

  return (
    <Editor
      key={`${id}-${generation}`}
      onRegenerated={() => setGeneration((g) => g + 1)}
      requestId={id}
      caseNumber={request.data.caseNumber}
      status={request.data.status}
      requestVersion={request.data.version}
      draft={draft.data}
      breadcrumbs={breadcrumbs}
    />
  )
}

type EditorProps = {
  onRegenerated: () => void
  requestId: string
  caseNumber: string
  status: string
  requestVersion: number
  draft: Draft
  breadcrumbs: { label: string; to?: string }[]
}

function Editor({ onRegenerated, requestId, caseNumber, status, requestVersion, draft, breadcrumbs }: EditorProps) {
  const navigate = useNavigate()
  const save = useSaveDraft(requestId)
  const regenerate = useGenerateDraft(requestId)
  const [confirmRegenerate, setConfirmRegenerate] = useState(false)
  const detect = useDetectRedactions(requestId)
  const changeStatus = useChangeStatus(requestId)
  const [content, setContent] = useState(draft.content)
  const [redactions, setRedactions] = useState<Redaction[]>(draft.redactions)
  const [version, setVersion] = useState(draft.version)
  const [mode, setMode] = useState<Mode>('review')
  const [dirty, setDirty] = useState(false)
  const [savedAt, setSavedAt] = useState<Date | null>(null)
  const [pendingExport, setPendingExport] = useState<ExportFormat | null>(null)
  const contentAtEditStart = useRef(draft.content)
  const counts = countByStatus(redactions)
  const placeholders = content.match(placeholderPattern)?.length ?? 0

  const blocker = useBlocker(({ currentLocation, nextLocation }) => dirty && currentLocation.pathname !== nextLocation.pathname)

  useEffect(() => {
    if (!dirty) return
    const warn = (event: BeforeUnloadEvent) => event.preventDefault()
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [dirty])

  const updateRedactions = (next: Redaction[]) => {
    setRedactions(next)
    setDirty(true)
  }

  const runDetect = useCallback(
    (text: string, existing: Redaction[]) =>
      detect.mutateAsync({ content: text, existing }).then((result) => {
        setRedactions(result.redactions)
        for (const warning of result.warnings) notifyError(new Error(warning), 'Uwaga')
        return result.redactions
      }),
    [detect],
  )

  const persist = useCallback(async () => {
    const saved = await save.mutateAsync({ content, redactions, version })
    setVersion(saved.version)
    setRedactions(saved.redactions)
    setDirty(false)
    setSavedAt(new Date())
    return saved
  }, [content, redactions, version, save])

  const saveNow = useCallback(async () => {
    try {
      if (mode === 'edit' && content !== contentAtEditStart.current) await runDetect(content, redactions)
      await persist()
      notifySuccess('Zapisano projekt odpowiedzi')
    } catch (error) {
      notifyError(error, 'Nie udało się zapisać projektu')
    }
  }, [mode, content, redactions, runDetect, persist])

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
        event.preventDefault()
        void saveNow()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [saveNow])

  const switchMode = async (next: Mode) => {
    if (next === mode) return
    if (mode === 'edit' && content !== contentAtEditStart.current) {
      try {
        await runDetect(content, redactions)
      } catch (error) {
        notifyError(error, 'Nie udało się zaktualizować anonimizacji')
      }
    }
    if (next === 'edit') contentAtEditStart.current = content
    setMode(next)
  }

  const doExport = async (format: ExportFormat) => {
    setPendingExport(null)
    try {
      if (dirty) await persist()
      await exportDraft(requestId, caseNumber, format)
    } catch (error) {
      notifyError(error, 'Nie udało się pobrać pliku')
    }
  }

  const nothingMarked = redactions.length === 0
  const requestExport = (format: ExportFormat) =>
    counts.suggested > 0 || placeholders > 0 || nothingMarked ? setPendingExport(format) : void doExport(format)

  const markPrepared = async () => {
    try {
      if (dirty) await persist()
      await changeStatus.mutateAsync({ status: 'ResponsePrepared', version: requestVersion })
      notifySuccess('Oznaczono odpowiedź jako przygotowaną')
      navigate(`/wnioski/${requestId}`)
    } catch (error) {
      notifyError(error)
    }
  }

  const saveLabel = save.isPending ? 'Zapisywanie…' : dirty ? 'Niezapisane zmiany' : savedAt ? `Zapisano o ${formatTime(savedAt)}` : 'Wszystkie zmiany zapisane'

  return (
    <>
      <PageHeader
        title="Projekt odpowiedzi"
        eyebrow={caseNumber}
        documentTitle={`Projekt odpowiedzi – ${caseNumber}`}
        breadcrumbs={breadcrumbs}
        actions={
          <>
            <Text fontSize="sm" color={dirty ? 'orange.fg' : 'fg.muted'} role="status" aria-live="polite" mr="1">
              {saveLabel}
            </Text>
            <Button onClick={saveNow} loading={save.isPending} disabled={!dirty && mode !== 'edit'}>
              <Save size={16} aria-hidden /> Zapisz
            </Button>
            <Menu.Root positioning={{ placement: 'bottom-end' }}>
              <Menu.Trigger asChild>
                <Button variant="outline">
                  <Download size={16} aria-hidden /> Pobierz i więcej <ChevronDown size={14} aria-hidden />
                </Button>
              </Menu.Trigger>
              <Portal>
                <Menu.Positioner>
                  <Menu.Content>
                    <Menu.Item value="docx" onSelect={() => requestExport('docx')}>
                      Dokument Word (.docx)
                    </Menu.Item>
                    <Menu.Item value="txt" onSelect={() => requestExport('txt')}>
                      Tekst (.txt)
                    </Menu.Item>
                    <Menu.Separator />
                    <Menu.Item value="regenerate" onSelect={() => setConfirmRegenerate(true)}>
                      <RefreshCw size={16} aria-hidden /> Przygotuj projekt ponownie
                    </Menu.Item>
                  </Menu.Content>
                </Menu.Positioner>
              </Portal>
            </Menu.Root>
            {status !== 'ResponsePrepared' && status !== 'ResponseSent' && (
              <Button variant="outline" onClick={markPrepared} loading={changeStatus.isPending}>
                <CheckCircle2 size={16} aria-hidden /> Odpowiedź gotowa
              </Button>
            )}
          </>
        }
      />

      <Alert.Root status="info" variant="subtle" mb="4">
        <Alert.Indicator />
        <Alert.Content>
          <Alert.Title>
            {draft.generatedByAi ? 'Projekt przygotował asystent AI.' : 'Projekt przygotowany z szablonu.'} Sprawdź merytorycznie
            każdą informację przed wysłaniem.
          </Alert.Title>
          {placeholders > 0 && (
            <Alert.Description>
              Pismo zawiera {placeholders} {plural(placeholders, 'miejsce', 'miejsca', 'miejsc')} oznaczone „DO UZUPEŁNIENIA”.
            </Alert.Description>
          )}
        </Alert.Content>
      </Alert.Root>

      <Flex mb="4" gap="3" align="center" wrap="wrap">
        <SegmentGroup.Root value={mode} onValueChange={(e) => void switchMode(e.value as Mode)} aria-label="Tryb pracy z projektem">
          <SegmentGroup.Indicator />
          <SegmentGroup.Item value="review">
            <SegmentGroup.ItemText display="inline-flex" alignItems="center" gap="1.5">
              <ShieldCheck size={16} aria-hidden /> Anonimizacja
            </SegmentGroup.ItemText>
            <SegmentGroup.ItemHiddenInput />
          </SegmentGroup.Item>
          <SegmentGroup.Item value="edit">
            <SegmentGroup.ItemText display="inline-flex" alignItems="center" gap="1.5">
              <PenLine size={16} aria-hidden /> Edycja treści
            </SegmentGroup.ItemText>
            <SegmentGroup.ItemHiddenInput />
          </SegmentGroup.Item>
          <SegmentGroup.Item value="preview">
            <SegmentGroup.ItemText display="inline-flex" alignItems="center" gap="1.5">
              <Eye size={16} aria-hidden /> Podgląd po anonimizacji
            </SegmentGroup.ItemText>
            <SegmentGroup.ItemHiddenInput />
          </SegmentGroup.Item>
        </SegmentGroup.Root>
        {detect.isPending && (
          <Text fontSize="sm" color="fg.muted" role="status">
            Aktualizuję anonimizację…
          </Text>
        )}
      </Flex>

      <Grid templateColumns={{ base: '1fr', xl: mode === 'review' ? 'minmax(0, 1fr) 360px' : '1fr' }} gap="6" alignItems="start">
        {mode === 'edit' ? (
          <Box maxW="820px" mx="auto" w="full">
            <Field.Root>
              <Field.Label srOnly>Treść projektu odpowiedzi</Field.Label>
              <Textarea
                value={content}
                onChange={(e) => {
                  setContent(e.target.value)
                  setDirty(true)
                }}
                autoresize
                minH="70vh"
                bg="bg.panel"
                px={{ base: 5, md: 12 }}
                py={{ base: 6, md: 12 }}
                fontSize="md"
                lineHeight="1.8"
                boxShadow="sm"
              />
              <Field.HelperText>
                Po powrocie do trybu anonimizacji zaznaczenia zostaną dopasowane do zmienionej treści, a nowe dane wykryte automatycznie.
              </Field.HelperText>
            </Field.Root>
          </Box>
        ) : (
          <RedactedDocument
            label="Treść projektu odpowiedzi"
            content={content}
            redactions={redactions}
            mode={mode}
            onToggle={(index) =>
              updateRedactions(withStatus(redactions, index, redactions[index].status === 'Approved' ? 'Suggested' : 'Approved'))
            }
            onSelectRange={(start, end) => updateRedactions(addManual(redactions, content, start, end))}
          />
        )}
        {mode === 'review' && (
          <Box position={{ xl: 'sticky' }} top={{ xl: '20' }} bg="bg.subtle" borderWidth="1px" borderColor="border" borderRadius="md" p="4">
            <RedactionPanel
              redactions={redactions}
              detecting={detect.isPending}
              onSetStatus={(index, next) => updateRedactions(withStatus(redactions, index, next))}
              onApproveAll={() => updateRedactions(approveAllSuggested(redactions))}
              onDetect={() =>
                runDetect(content, redactions)
                  .then(() => setDirty(true))
                  .catch((e) => notifyError(e))
              }
              onAddPhrase={(phrase) => {
                const result = addPhrase(redactions, content, phrase)
                if (result.added > 0) updateRedactions(result.redactions)
                return result.added
              }}
            />
          </Box>
        )}
      </Grid>

      <ConfirmDialog
        open={pendingExport !== null}
        title="Pobrać pismo mimo uwag?"
        confirmLabel="Pobierz mimo to"
        onCancel={() => setPendingExport(null)}
        onConfirm={() => pendingExport && void doExport(pendingExport)}
      >
        <Text>
          {counts.suggested > 0 &&
            `${counts.suggested} ${plural(counts.suggested, 'fragment czeka', 'fragmenty czekają', 'fragmentów czeka')} na decyzję — niezweryfikowane dane NIE zostaną ukryte. `}
          {placeholders > 0 && `Pismo zawiera ${placeholders} ${plural(placeholders, 'miejsce', 'miejsca', 'miejsc')} do uzupełnienia. `}
          {nothingMarked && 'W piśmie nie wskazano żadnych danych do anonimizacji — upewnij się, że nie zawiera danych osobowych ani informacji chronionych.'}
        </Text>
      </ConfirmDialog>

      <ConfirmDialog
        open={confirmRegenerate}
        title="Przygotować projekt ponownie?"
        confirmLabel="Zastąp obecny projekt"
        destructive
        loading={regenerate.isPending}
        onCancel={() => setConfirmRegenerate(false)}
        onConfirm={() =>
          regenerate.mutate(version, {
            onSuccess: (result) => {
              setConfirmRegenerate(false)
              setDirty(false)
              for (const warning of result.warnings) notifyError(new Error(warning), 'Uwaga')
              onRegenerated()
            },
            onError: (e) => {
              setConfirmRegenerate(false)
              notifyError(e, 'Nie udało się przygotować projektu')
            },
          })
        }
      >
        Nowy projekt powstanie na podstawie aktualnych odpowiedzi wydziałów i zastąpi obecną treść oraz decyzje dotyczące
        anonimizacji. Niezapisane zmiany zostaną utracone.
      </ConfirmDialog>

      <ConfirmDialog
        open={blocker.state === 'blocked'}
        title="Masz niezapisane zmiany"
        confirmLabel="Wyjdź bez zapisywania"
        destructive
        onCancel={() => blocker.reset?.()}
        onConfirm={() => blocker.proceed?.()}
      >
        Jeśli opuścisz edytor, zmiany w projekcie odpowiedzi i anonimizacji zostaną utracone.
      </ConfirmDialog>
    </>
  )
}
