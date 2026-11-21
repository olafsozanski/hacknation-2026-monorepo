import {
  Alert,
  Box,
  Button,
  Card,
  Field,
  FileUpload,
  Flex,
  Grid,
  Heading,
  Icon,
  Menu,
  Portal,
  SegmentGroup,
  Separator,
  Stack,
  Tag,
  Text,
  Textarea,
} from '@chakra-ui/react'
import { ChevronDown, Download, Eye, FileText, FileUp, RotateCcw, ShieldCheck } from 'lucide-react'
import { useState } from 'react'
import type { AnonymizationResult, Redaction } from '@/api/types'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingState } from '@/components/common/States'
import { RedactedDocument, type DocumentMode } from '@/components/redaction/RedactedDocument'
import { RedactionPanel } from '@/components/redaction/RedactionPanel'
import { addManual, addPhrase, approveAllSuggested, countByStatus, withStatus } from '@/components/redaction/redactions'
import { notifyError } from '@/components/ui/toaster'
import { plural } from '@/lib/dates'
import { AnonymizationWalkthrough } from './AnonymizationWalkthrough'
import { exportAnonymized, useAnalyzeDocument } from './api'

const accept = ['application/pdf', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 'text/plain']

type Format = 'docx' | 'txt'

const howItWorks = [
  { title: 'Dodaj dokument', text: 'Prześlij plik PDF, DOCX lub TXT albo wklej tekst.' },
  { title: 'Zdecyduj, co ukryć', text: 'Kliknij dane zaznaczone przez asystenta albo zaznacz myszą dowolny inny fragment.' },
  { title: 'Pobierz wynik', text: 'Pobierz dokument z ukrytymi danymi jako plik Word lub tekstowy.' },
]

function HowItWorks() {
  return (
    <Box as="section" aria-labelledby="how-it-works-heading">
      <Heading as="h2" id="how-it-works-heading" size="sm" mb="3">
        Jak to działa
      </Heading>
      <Grid as="ol" templateColumns={{ base: '1fr', md: 'repeat(3, minmax(0, 1fr))' }} gap="3" m="0" p="0" listStyleType="none">
        {howItWorks.map((step, index) => (
          <Flex as="li" key={step.title} gap="3" align="flex-start" bg="bg.panel" borderWidth="1px" borderColor="border" borderRadius="md" p="3">
            <Flex
              flexShrink="0"
              boxSize="7"
              align="center"
              justify="center"
              borderRadius="full"
              bg="brand.solid"
              color="brand.contrast"
              fontWeight="bold"
              fontSize="sm"
              aria-hidden
            >
              {index + 1}
            </Flex>
            <Box>
              <Text fontWeight="semibold">{step.title}</Text>
              <Text fontSize="sm" color="fg.muted">
                {step.text}
              </Text>
            </Box>
          </Flex>
        ))}
      </Grid>
    </Box>
  )
}

function SourceForm({ busy, error, onAnalyze }: { busy: boolean; error: string | null; onAnalyze: (source: File | string) => void }) {
  const [text, setText] = useState('')
  const [rejection, setRejection] = useState<string | null>(null)
  const message = rejection ?? error
  return (
    <Stack gap="5" maxW="4xl">
      <HowItWorks />
      {message && (
        <Alert.Root status="error" variant="outline" role="alert">
          <Alert.Indicator />
          <Alert.Title>{message}</Alert.Title>
        </Alert.Root>
      )}
      <Card.Root variant="outline" bg="bg.panel">
        <Card.Body gap="4">
          <Heading as="h2" size="md">
            Prześlij dokument
          </Heading>
          <FileUpload.Root
            accept={accept}
            maxFiles={1}
            maxFileSize={20 * 1024 * 1024}
            disabled={busy}
            translations={{ dropzone: 'Przeciągnij tutaj dokument lub kliknij, aby wybrać PDF, DOCX lub TXT, do 20 MB' }}
            onFileAccept={(details) => {
              setRejection(null)
              if (details.files[0]) onAnalyze(details.files[0])
            }}
            onFileReject={(details) =>
              details.files.length > 0 && setRejection('Nieobsługiwany plik. Prześlij PDF, DOCX lub TXT do 20 MB.')
            }
          >
            <FileUpload.HiddenInput aria-label="Dokument do anonimizacji (PDF, DOCX lub TXT)" />
            <FileUpload.Dropzone
              w="full"
              minH="44"
              borderWidth="2px"
              borderStyle="dashed"
              borderColor="border.emphasized"
              bg="bg.subtle"
              _hover={{ borderColor: 'brand.solid', bg: 'brand.subtle' }}
              _dragging={{ borderColor: 'brand.solid', bg: 'brand.subtle' }}
            >
              <Icon size="2xl" color="brand.fg">
                <FileUp aria-hidden />
              </Icon>
              <FileUpload.DropzoneContent>
                <Text fontWeight="semibold" fontSize="lg">
                  Przeciągnij tutaj dokument lub kliknij, aby wybrać
                </Text>
                <Text color="fg.muted">PDF, DOCX lub TXT, do 20 MB</Text>
              </FileUpload.DropzoneContent>
            </FileUpload.Dropzone>
          </FileUpload.Root>
        </Card.Body>
      </Card.Root>
      <Flex align="center" gap="3" color="fg.muted" fontSize="sm">
        <Separator flex="1" />
        <Text>lub</Text>
        <Separator flex="1" />
      </Flex>
      <Card.Root variant="outline" bg="bg.panel">
        <Card.Body gap="3">
          <Heading as="h2" size="md">
            Wklej tekst
          </Heading>
          <Field.Root>
            <Field.Label srOnly>Tekst do anonimizacji</Field.Label>
            <Textarea rows={8} value={text} onChange={(e) => setText(e.target.value)} disabled={busy} placeholder="Np. treść pisma lub odpowiedzi wydziału" />
          </Field.Root>
          <Box>
            <Button variant="outline" onClick={() => onAnalyze(text.trim())} disabled={busy || text.trim().length < 3}>
              <ShieldCheck size={16} aria-hidden /> Wykryj dane do ukrycia
            </Button>
          </Box>
        </Card.Body>
      </Card.Root>
    </Stack>
  )
}

export function AnonymizationPage() {
  const analyze = useAnalyzeDocument()
  const [result, setResult] = useState<AnonymizationResult | null>(null)
  const [redactions, setRedactions] = useState<Redaction[]>([])
  const [mode, setMode] = useState<DocumentMode>('review')
  const [pendingExport, setPendingExport] = useState<Format | null>(null)
  const counts = countByStatus(redactions)
  const needsConfirmation = counts.suggested > 0 || counts.approved === 0

  const start = (source: File | string) =>
    analyze.mutate(source, {
      onSuccess: (data) => {
        setResult(data)
        setRedactions(data.redactions)
        setMode('review')
        for (const warning of data.warnings) notifyError(new Error(warning), 'Uwaga')
      },
    })

  const doExport = async (format: Format) => {
    setPendingExport(null)
    if (!result) return
    try {
      await exportAnonymized({ content: result.text, redactions, format, fileName: result.fileName })
    } catch (error) {
      notifyError(error, 'Nie udało się pobrać dokumentu')
    }
  }

  const reset = () => {
    setResult(null)
    setRedactions([])
    analyze.reset()
  }

  return (
    <>
      <PageHeader
        title="Anonimizacja dokumentu"
        description="Sprawdź dowolny dokument przed udostępnieniem — asystent wskaże dane osobowe i inne informacje chronione, a Ty zdecydujesz, co ukryć. Dokument nie jest zapisywany w systemie."
        actions={
          result && (
            <>
              {result.text && <AnonymizationWalkthrough onStart={() => setMode('review')} />}
              <Button variant="ghost" onClick={reset}>
                <RotateCcw size={16} aria-hidden /> Nowy dokument
              </Button>
              <Menu.Root positioning={{ placement: 'bottom-end' }}>
                <Menu.Trigger asChild>
                  <Button data-walkthrough="download">
                    <Download size={16} aria-hidden /> Pobierz zanonimizowany <ChevronDown size={14} aria-hidden />
                  </Button>
                </Menu.Trigger>
                <Portal>
                  <Menu.Positioner>
                    <Menu.Content>
                      <Menu.Item value="docx" onSelect={() => (needsConfirmation ? setPendingExport('docx') : void doExport('docx'))}>
                        Dokument Word (.docx)
                      </Menu.Item>
                      <Menu.Item value="txt" onSelect={() => (needsConfirmation ? setPendingExport('txt') : void doExport('txt'))}>
                        Tekst (.txt)
                      </Menu.Item>
                    </Menu.Content>
                  </Menu.Positioner>
                </Portal>
              </Menu.Root>
            </>
          )
        }
      />

      {analyze.isPending ? (
        <LoadingState label="Wyszukuję dane do anonimizacji…" />
      ) : !result ? (
        <SourceForm busy={analyze.isPending} error={analyze.error?.message ?? null} onAnalyze={start} />
      ) : !result.text ? (
        <Alert.Root status="warning" variant="outline">
          <Alert.Indicator />
          <Alert.Title>Nie odczytano tekstu z dokumentu — może to być skan. Wklej treść ręcznie.</Alert.Title>
        </Alert.Root>
      ) : (
        <Stack gap="4">
          <Flex gap="3" align="center" wrap="wrap">
            {result.fileName && (
              <Tag.Root size="lg" variant="outline">
                <Tag.StartElement>
                  <FileText size={14} aria-hidden />
                </Tag.StartElement>
                <Tag.Label>{result.fileName}</Tag.Label>
              </Tag.Root>
            )}
            <Text fontSize="sm" color="fg.muted" role="status" aria-live="polite">
              Wykryto {redactions.length} {plural(redactions.length, 'fragment', 'fragmenty', 'fragmentów')}
              {result.aiUsed ? ' (reguły i analiza AI)' : ' (reguły)'}
            </Text>
            <SegmentGroup.Root
              value={mode}
              onValueChange={(e) => setMode(e.value as DocumentMode)}
              aria-label="Widok dokumentu"
              ms={{ md: 'auto' }}
              data-walkthrough="view"
            >
              <SegmentGroup.Indicator />
              <SegmentGroup.Item value="review">
                <SegmentGroup.ItemText display="inline-flex" alignItems="center" gap="1.5">
                  <ShieldCheck size={16} aria-hidden /> Weryfikacja
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
          </Flex>
          <Grid templateColumns={{ base: '1fr', xl: mode === 'review' ? 'minmax(0, 1fr) 360px' : '1fr' }} gap="6" alignItems="start">
            <Box minW="0" data-walkthrough="document">
              <RedactedDocument
                label="Treść dokumentu"
                content={result.text}
                redactions={redactions}
                mode={mode}
                onToggle={(index) =>
                  setRedactions(withStatus(redactions, index, redactions[index].status === 'Approved' ? 'Suggested' : 'Approved'))
                }
                onSelectRange={(start, end) => setRedactions(addManual(redactions, result.text, start, end))}
              />
            </Box>
            {mode === 'review' && (
              <Box
                position={{ xl: 'sticky' }}
                top={{ xl: '20' }}
                bg="bg.subtle"
                borderWidth="1px"
                borderColor="border"
                borderRadius="md"
                p="4"
                data-walkthrough="panel"
              >
                <RedactionPanel
                  redactions={redactions}
                  detecting={false}
                  onSetStatus={(index, status) => setRedactions(withStatus(redactions, index, status))}
                  onApproveAll={() => setRedactions(approveAllSuggested(redactions))}
                  onAddPhrase={(phrase) => {
                    const next = addPhrase(redactions, result.text, phrase)
                    if (next.added > 0) setRedactions(next.redactions)
                    return next.added
                  }}
                />
              </Box>
            )}
          </Grid>
        </Stack>
      )}

      <ConfirmDialog
        open={pendingExport !== null}
        title="Pobrać dokument?"
        confirmLabel="Pobierz mimo to"
        onCancel={() => setPendingExport(null)}
        onConfirm={() => pendingExport && void doExport(pendingExport)}
      >
        {counts.suggested > 0
          ? `${counts.suggested} ${plural(counts.suggested, 'fragment czeka', 'fragmenty czekają', 'fragmentów czeka')} na decyzję — niezweryfikowane dane NIE zostaną ukryte.`
          : 'Żaden fragment nie zostanie ukryty. Upewnij się, że dokument nie zawiera danych osobowych ani informacji chronionych.'}
      </ConfirmDialog>
    </>
  )
}
