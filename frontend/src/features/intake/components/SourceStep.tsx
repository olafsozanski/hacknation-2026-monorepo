import { Alert, Box, Button, Card, Field, FileUpload, Flex, Heading, Icon, Separator, Stack, Text, Textarea } from '@chakra-ui/react'
import { ClipboardPaste, FileUp, PencilLine } from 'lucide-react'
import { useState } from 'react'
import { LinkButton } from '@/components/common/LinkButton'

const accept = [
  'application/pdf',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'text/plain',
]

type SourceStepProps = {
  busy: boolean
  error: string | null
  onFile: (file: File) => void
  onText: (text: string) => void
  onManual: () => void
}

export function SourceStep({ busy, error, onFile, onText, onManual }: SourceStepProps) {
  const [pasted, setPasted] = useState('')
  const [rejection, setRejection] = useState<string | null>(null)
  const message = rejection ?? error

  return (
    <Stack gap="5" maxW="4xl">
      {message && (
        <Alert.Root status="error" variant="outline" role="alert">
          <Alert.Indicator />
          <Alert.Content>
            <Alert.Title>Nie udało się przetworzyć wniosku</Alert.Title>
            <Alert.Description>{message}</Alert.Description>
          </Alert.Content>
        </Alert.Root>
      )}
      <Card.Root variant="outline" bg="bg.panel">
        <Card.Body gap="4">
          <Heading as="h2" size="md">
            Prześlij plik wniosku
          </Heading>
          <FileUpload.Root
            accept={accept}
            maxFiles={1}
            maxFileSize={20 * 1024 * 1024}
            disabled={busy}
            translations={{ dropzone: 'Przeciągnij tutaj plik lub kliknij, aby wybrać PDF, DOCX lub TXT, do 20 MB' }}
            onFileAccept={(details) => {
              setRejection(null)
              if (details.files[0]) onFile(details.files[0])
            }}
            onFileReject={(details) => {
              if (details.files.length === 0) return
              const code = details.files[0].errors[0]
              setRejection(
                code === 'FILE_TOO_LARGE'
                  ? 'Plik jest za duży. Maksymalny rozmiar to 20 MB.'
                  : 'Nieobsługiwany format pliku. Prześlij plik PDF, DOCX lub TXT.',
              )
            }}
          >
            <FileUpload.HiddenInput aria-label="Plik wniosku (PDF, DOCX lub TXT)" />
            <FileUpload.Dropzone
              w="full"
              minH="56"
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
                  Przeciągnij tutaj plik lub kliknij, aby wybrać
                </Text>
                <Text color="fg.muted">PDF, DOCX lub TXT, do 20 MB</Text>
              </FileUpload.DropzoneContent>
            </FileUpload.Dropzone>
          </FileUpload.Root>
          <Text fontSize="sm" color="fg.muted">
            Asystent odczyta treść, zaproponuje przedmiot i dane wnioskodawcy, wyliczy termin oraz wyszuka podobne sprawy w
            rejestrze i archiwum.
          </Text>
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
            Wklej treść wniosku
          </Heading>
          <Field.Root>
            <Field.Label srOnly>Treść wniosku</Field.Label>
            <Textarea
              rows={6}
              placeholder="Np. treść wiadomości e-mail lub pisma przesłanego przez ePUAP"
              value={pasted}
              onChange={(e) => setPasted(e.target.value)}
              disabled={busy}
            />
          </Field.Root>
          <Flex gap="3" wrap="wrap" justify="space-between" align="center">
            <Button variant="outline" onClick={() => onText(pasted.trim())} disabled={busy || pasted.trim().length < 20}>
              <ClipboardPaste size={16} aria-hidden /> Analizuj treść
            </Button>
            <LinkButton size="md" onClick={onManual} disabled={busy}>
              <PencilLine size={16} aria-hidden /> Wypełnij formularz ręcznie
            </LinkButton>
          </Flex>
        </Card.Body>
      </Card.Root>

      {busy && (
        <Box role="status" aria-live="polite" srOnly>
          Analizuję wniosek…
        </Box>
      )}
    </Stack>
  )
}
