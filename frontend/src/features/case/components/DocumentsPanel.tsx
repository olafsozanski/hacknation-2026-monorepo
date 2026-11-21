import { Box, Button, Collapsible, Field, FileUpload, Flex, IconButton, NativeSelect, Stack, Tag, Text } from '@chakra-ui/react'
import { useQuery } from '@tanstack/react-query'
import { ChevronDown, Download, FileText, Trash2, Upload } from 'lucide-react'
import { useState } from 'react'
import { downloadFile } from '@/api/client'
import type { DocumentInfo, DocumentKind, RequestDetail } from '@/api/types'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { Panel } from '@/components/common/Panel'
import { EmptyHint } from '@/components/common/States'
import { notifyError, notifySuccess } from '@/components/ui/toaster'
import { formatDateTime } from '@/lib/dates'
import { documentKindLabels } from '@/lib/labels'
import { documentTextQuery, useDeleteDocument, useUploadDocument } from '../api'

const accept = ['application/pdf', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 'text/plain']

function formatSize(bytes: number) {
  return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

function DocumentText({ documentId }: { documentId: string }) {
  const text = useQuery(documentTextQuery(documentId))
  if (text.isPending) return <Text color="fg.muted">Wczytywanie…</Text>
  return (
    <Box bg="bg.subtle" borderRadius="sm" p="3" maxH="80" overflowY="auto" fontSize="sm" whiteSpace="pre-wrap" lineHeight="1.6">
      {text.data?.text || 'Brak odczytanego tekstu.'}
    </Box>
  )
}

function DocumentItem({ request, document }: { request: RequestDetail; document: DocumentInfo }) {
  const remove = useDeleteDocument(request.id)
  const [confirming, setConfirming] = useState(false)
  return (
    <Box as="li" listStyleType="none" borderWidth="1px" borderColor="border" borderRadius="md" p="3">
      <Collapsible.Root>
        <Flex gap="3" align="flex-start" wrap="wrap">
          <Box color="brand.fg" pt="0.5">
            <FileText size={20} aria-hidden />
          </Box>
          <Box flex="1" minW="48">
            <Text fontWeight="semibold" wordBreak="break-all">
              {document.fileName}
            </Text>
            <Flex gap="2" mt="1" wrap="wrap" align="center" fontSize="xs" color="fg.muted">
              <Tag.Root size="sm" variant="subtle" colorPalette={document.kind === 'DepartmentReply' ? 'green' : 'gray'}>
                <Tag.Label>
                  {documentKindLabels[document.kind]}
                  {document.departmentSymbol ? ` · ${document.departmentSymbol}` : ''}
                </Tag.Label>
              </Tag.Root>
              <Text>
                {formatSize(document.sizeBytes)} · {formatDateTime(document.uploadedAt)} · {document.uploadedBy}
              </Text>
            </Flex>
          </Box>
          <Flex gap="1">
            {document.hasText && (
              <Collapsible.Trigger asChild>
                <Button size="xs" variant="ghost">
                  <ChevronDown size={14} aria-hidden /> Tekst
                </Button>
              </Collapsible.Trigger>
            )}
            <IconButton
              size="xs"
              variant="ghost"
              aria-label={`Pobierz ${document.fileName}`}
              onClick={() => downloadFile(`/api/documents/${document.id}/content`, document.fileName).catch((e) => notifyError(e))}
            >
              <Download size={14} aria-hidden />
            </IconButton>
            {document.kind !== 'Request' && (
              <IconButton size="xs" variant="ghost" colorPalette="red" aria-label={`Usuń ${document.fileName}`} onClick={() => setConfirming(true)}>
                <Trash2 size={14} aria-hidden />
              </IconButton>
            )}
          </Flex>
        </Flex>
        <Collapsible.Content>
          <Box mt="3">
            <DocumentText documentId={document.id} />
          </Box>
        </Collapsible.Content>
      </Collapsible.Root>
      <ConfirmDialog
        open={confirming}
        title="Usunąć dokument?"
        confirmLabel="Usuń dokument"
        destructive
        loading={remove.isPending}
        onCancel={() => setConfirming(false)}
        onConfirm={() =>
          remove.mutate(document.id, {
            onSuccess: () => setConfirming(false),
            onError: (e) => notifyError(e),
          })
        }
      >
        {`Plik „${document.fileName}” zostanie trwale usunięty ze sprawy.`}
      </ConfirmDialog>
    </Box>
  )
}

export function DocumentsPanel({ request }: { request: RequestDetail }) {
  const upload = useUploadDocument(request.id)
  const [departmentId, setDepartmentId] = useState('')
  const [kind, setKind] = useState<DocumentKind>('DepartmentReply')
  const documents = request.documents.filter((d) => d.kind !== 'Request')

  const onFile = (file: File) =>
    upload.mutate(
      { file, kind, departmentId: kind === 'DepartmentReply' ? departmentId || null : null },
      {
        onSuccess: () => notifySuccess('Dodano dokument', file.name),
        onError: (e) => notifyError(e, 'Nie udało się dodać dokumentu'),
      },
    )

  return (
    <Panel
      id="odpowiedzi"
      step={2}
      title="Odpowiedzi wydziałów i załączniki"
      description="Dodaj pliki otrzymane z wydziałów — staną się podstawą projektu odpowiedzi."
    >
      <Stack gap="4">
        {documents.length > 0 ? (
          <Stack as="ul" gap="2" m="0" p="0">
            {documents.map((d) => (
              <DocumentItem key={d.id} request={request} document={d} />
            ))}
          </Stack>
        ) : (
          <EmptyHint title="Brak odpowiedzi wydziałów" description="Gdy wydział odpowie, dodaj tutaj otrzymany plik." icon={<FileText aria-hidden />} />
        )}
        <Flex gap="3" align="flex-end" wrap="wrap" bg="bg.subtle" p="3" borderRadius="md" borderWidth="1px" borderColor="border.muted">
          <Field.Root w="auto" minW="44">
            <Field.Label fontSize="sm">Rodzaj dokumentu</Field.Label>
            <NativeSelect.Root size="sm">
              <NativeSelect.Field value={kind} onChange={(e) => setKind(e.target.value as DocumentKind)}>
                <option value="DepartmentReply">{documentKindLabels.DepartmentReply}</option>
                <option value="Attachment">{documentKindLabels.Attachment}</option>
              </NativeSelect.Field>
              <NativeSelect.Indicator />
            </NativeSelect.Root>
          </Field.Root>
          {kind === 'DepartmentReply' && (
            <Field.Root w="auto" minW="56" flex="1">
              <Field.Label fontSize="sm">Wydział</Field.Label>
              <NativeSelect.Root size="sm">
                <NativeSelect.Field value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
                  <option value="">— nie wskazano —</option>
                  {request.departments.map((d) => (
                    <option key={d.departmentId} value={d.departmentId}>
                      {d.symbol} — {d.name}
                    </option>
                  ))}
                </NativeSelect.Field>
                <NativeSelect.Indicator />
              </NativeSelect.Root>
            </Field.Root>
          )}
          <FileUpload.Root
            accept={accept}
            maxFiles={1}
            maxFileSize={20 * 1024 * 1024}
            w="auto"
            onFileAccept={(details) => details.files[0] && onFile(details.files[0])}
            onFileReject={(details) =>
              details.files.length > 0 && notifyError(new Error('Dozwolone są pliki PDF, DOCX lub TXT do 20 MB.'), 'Nie można dodać pliku')
            }
          >
            <FileUpload.HiddenInput aria-label="Wybierz plik dokumentu" />
            <FileUpload.Trigger asChild>
              <Button size="sm" loading={upload.isPending} loadingText="Przesyłanie…">
                <Upload size={14} aria-hidden /> Dodaj plik
              </Button>
            </FileUpload.Trigger>
          </FileUpload.Root>
        </Flex>
      </Stack>
    </Panel>
  )
}
