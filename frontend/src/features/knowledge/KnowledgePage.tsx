import {
  Badge,
  Button,
  CloseButton,
  Dialog,
  Field,
  FileUpload,
  Flex,
  IconButton,
  Input,
  NativeSelect,
  Portal,
  Stack,
  Table,
  Text,
} from '@chakra-ui/react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BookOpen, Download, Trash2, Upload } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { ApiError, api, downloadFile } from '@/api/client'
import type { KnowledgeDocument, KnowledgeKind } from '@/api/types'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { PageHeader } from '@/components/common/PageHeader'
import { EmptyHint, ErrorState, LoadingState } from '@/components/common/States'
import { notifyError, notifySuccess } from '@/components/ui/toaster'
import { useSystemInfo } from '@/features/platform/api'
import { formatDateTime } from '@/lib/dates'
import { knowledgeKindLabels } from '@/lib/labels'

const knowledgeQueryKey = ['knowledge']

function UploadDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [file, setFile] = useState<File | null>(null)
  const [title, setTitle] = useState('')
  const [kind, setKind] = useState<KnowledgeKind>('Law')
  const [error, setError] = useState<string | null>(null)
  const upload = useMutation({
    mutationFn: () => {
      const form = new FormData()
      form.append('file', file!)
      form.append('kind', kind)
      if (title.trim()) form.append('title', title.trim())
      return api<KnowledgeDocument>('/api/knowledge', { method: 'POST', body: form })
    },
    onSuccess: (doc) => {
      void queryClient.invalidateQueries({ queryKey: knowledgeQueryKey })
      notifySuccess('Dodano dokument do bazy wiedzy', doc.title)
      setFile(null)
      setTitle('')
      onClose()
    },
    onError: (e) => setError(e instanceof ApiError ? (Object.values(e.fieldErrors)[0] ?? e.message) : 'Nie udało się dodać dokumentu.'),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!file) {
      setError('Wybierz plik.')
      return
    }
    setError(null)
    upload.mutate()
  }

  return (
    <Dialog.Root open={open} onOpenChange={(e) => !e.open && onClose()}>
      <Portal>
        <Dialog.Backdrop />
        <Dialog.Positioner>
          <Dialog.Content as="form" onSubmit={submit}>
            <Dialog.Header>
              <Dialog.Title>Dodaj dokument do bazy wiedzy</Dialog.Title>
            </Dialog.Header>
            <Dialog.Body>
              <Stack gap="4">
                <Field.Root required invalid={!!error}>
                  <Field.Label>Plik (PDF, DOCX, TXT)</Field.Label>
                  <FileUpload.Root
                    maxFiles={1}
                    maxFileSize={20 * 1024 * 1024}
                    accept={['application/pdf', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 'text/plain']}
                    onFileAccept={(d) => {
                      setFile(d.files[0] ?? null)
                      setError(null)
                    }}
                    onFileReject={(d) => d.files.length > 0 && setError('Dozwolone są pliki PDF, DOCX lub TXT do 20 MB.')}
                  >
                    <FileUpload.HiddenInput />
                    <FileUpload.Trigger asChild>
                      <Button variant="outline" size="sm">
                        <Upload size={14} aria-hidden /> {file ? 'Zmień plik' : 'Wybierz plik'}
                      </Button>
                    </FileUpload.Trigger>
                  </FileUpload.Root>
                  {file && <Text fontSize="sm">{file.name}</Text>}
                  <Field.ErrorText>{error}</Field.ErrorText>
                </Field.Root>
                <Field.Root>
                  <Field.Label>Tytuł</Field.Label>
                  <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Domyślnie nazwa pliku" maxLength={500} />
                </Field.Root>
                <Field.Root>
                  <Field.Label>Rodzaj dokumentu</Field.Label>
                  <NativeSelect.Root>
                    <NativeSelect.Field value={kind} onChange={(e) => setKind(e.target.value as KnowledgeKind)}>
                      {Object.entries(knowledgeKindLabels).map(([value, label]) => (
                        <option key={value} value={value}>
                          {label}
                        </option>
                      ))}
                    </NativeSelect.Field>
                    <NativeSelect.Indicator />
                  </NativeSelect.Root>
                </Field.Root>
              </Stack>
            </Dialog.Body>
            <Dialog.Footer>
              <Button variant="outline" onClick={onClose}>
                Anuluj
              </Button>
              <Button type="submit" loading={upload.isPending}>
                Dodaj
              </Button>
            </Dialog.Footer>
            <Dialog.CloseTrigger asChild>
              <CloseButton size="sm" aria-label="Zamknij" />
            </Dialog.CloseTrigger>
          </Dialog.Content>
        </Dialog.Positioner>
      </Portal>
    </Dialog.Root>
  )
}

export function KnowledgePage() {
  const queryClient = useQueryClient()
  const { data: info } = useSystemInfo()
  const documents = useQuery({ queryKey: knowledgeQueryKey, queryFn: () => api<KnowledgeDocument[]>('/api/knowledge') })
  const [uploading, setUploading] = useState(false)
  const [deleting, setDeleting] = useState<KnowledgeDocument | null>(null)
  const remove = useMutation({
    mutationFn: (id: string) => api<void>(`/api/knowledge/${id}`, { method: 'DELETE' }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: knowledgeQueryKey })
      setDeleting(null)
    },
    onError: (e) => notifyError(e),
  })

  return (
    <>
      <PageHeader
        title="Baza wiedzy"
        description="Ustawy, zarządzenia, wytyczne i archiwalne odpowiedzi, z których asystent korzysta przy wyszukiwaniu podobnych spraw i przygotowywaniu projektów pism."
        actions={
          <Button onClick={() => setUploading(true)}>
            <Upload size={16} aria-hidden /> Dodaj dokument
          </Button>
        }
      />
      {documents.isPending ? (
        <LoadingState />
      ) : documents.isError ? (
        <ErrorState error={documents.error} onRetry={() => documents.refetch()} />
      ) : documents.data.length === 0 ? (
        <EmptyHint
          icon={<BookOpen aria-hidden />}
          title="Baza wiedzy jest pusta"
          description="Dodaj np. tekst ustawy o dostępie do informacji publicznej, KPA lub zarządzenia Prezydenta Miasta."
        />
      ) : (
        <Table.ScrollArea borderWidth="1px" borderColor="border" borderRadius="md" bg="bg.panel">
          <Table.Root size="md">
            <Table.Caption srOnly>Dokumenty w bazie wiedzy</Table.Caption>
            <Table.Header>
              <Table.Row bg="bg.muted">
                <Table.ColumnHeader>Tytuł</Table.ColumnHeader>
                <Table.ColumnHeader>Rodzaj</Table.ColumnHeader>
                <Table.ColumnHeader>Indeksowanie</Table.ColumnHeader>
                <Table.ColumnHeader>Dodano</Table.ColumnHeader>
                <Table.ColumnHeader>
                  <Text as="span" srOnly>Akcje</Text>
                </Table.ColumnHeader>
              </Table.Row>
            </Table.Header>
            <Table.Body>
              {documents.data.map((doc) => (
                <Table.Row key={doc.id}>
                  <Table.Cell>
                    <Text fontWeight="semibold">{doc.title}</Text>
                    {doc.fileName && (
                      <Text fontSize="sm" color="fg.muted">
                        {doc.fileName} · {doc.chunkCount} fragm.
                      </Text>
                    )}
                  </Table.Cell>
                  <Table.Cell>
                    <Badge variant="outline">{knowledgeKindLabels[doc.kind]}</Badge>
                  </Table.Cell>
                  <Table.Cell>
                    {doc.indexed ? (
                      <Badge colorPalette="green" variant="subtle">
                        Gotowy do wyszukiwania semantycznego
                      </Badge>
                    ) : info?.ai.enabled ? (
                      <Badge colorPalette="orange" variant="subtle">
                        W kolejce do indeksowania
                      </Badge>
                    ) : (
                      <Badge variant="subtle">Wyszukiwanie pełnotekstowe</Badge>
                    )}
                  </Table.Cell>
                  <Table.Cell fontSize="sm" whiteSpace="nowrap">
                    {formatDateTime(doc.createdAt)}
                    <Text color="fg.muted">{doc.createdBy}</Text>
                  </Table.Cell>
                  <Table.Cell textAlign="end" whiteSpace="nowrap">
                    <Flex gap="1" justify="flex-end">
                      {doc.fileName && (
                        <IconButton
                          size="sm"
                          variant="ghost"
                          aria-label={`Pobierz ${doc.title}`}
                          onClick={() => downloadFile(`/api/knowledge/${doc.id}/content`, doc.fileName!).catch((e) => notifyError(e))}
                        >
                          <Download size={16} aria-hidden />
                        </IconButton>
                      )}
                      <IconButton size="sm" variant="ghost" colorPalette="red" aria-label={`Usuń ${doc.title}`} onClick={() => setDeleting(doc)}>
                        <Trash2 size={16} aria-hidden />
                      </IconButton>
                    </Flex>
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table.Root>
        </Table.ScrollArea>
      )}
      <UploadDialog open={uploading} onClose={() => setUploading(false)} />
      <ConfirmDialog
        open={deleting !== null}
        title="Usunąć dokument z bazy wiedzy?"
        confirmLabel="Usuń"
        destructive
        loading={remove.isPending}
        onCancel={() => setDeleting(null)}
        onConfirm={() => deleting && remove.mutate(deleting.id)}
      >
        {`Dokument „${deleting?.title ?? ''}” przestanie być uwzględniany przy wyszukiwaniu i tworzeniu projektów.`}
      </ConfirmDialog>
    </>
  )
}
