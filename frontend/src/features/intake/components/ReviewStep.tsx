import {
  Alert,
  Badge,
  Box,
  Button,
  Collapsible,
  Field,
  Flex,
  Grid,
  List,
  NativeSelect,
  Stack,
  Tag,
  Text,
  Textarea,
} from '@chakra-ui/react'
import { useQuery } from '@tanstack/react-query'
import { ChevronDown, FileText, Sparkles } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { ApiError } from '@/api/client'
import type { IntakeResult } from '@/api/types'
import { Panel } from '@/components/common/Panel'
import { DepartmentSuggestionList } from '@/components/domain/DepartmentSuggestionList'
import { RequestFormFields, type RequestFormField, type RequestFormValues } from '@/components/domain/RequestFormFields'
import { SimilarCasesList } from '@/components/domain/SimilarCasesList'
import { notifySuccess } from '@/components/ui/toaster'
import { useCurrentUser } from '@/features/auth/session'
import { useDepartments, useUsers } from '@/features/platform/api'
import { formatDate, todayIso } from '@/lib/dates'
import { deadlinePreviewQuery, useCreateRequest } from '../api'

type ReviewStepProps = {
  intake: IntakeResult | null
  onRestart: () => void
}

function initialValues(intake: IntakeResult | null, userId: string): RequestFormValues {
  const s = intake?.suggestion
  return {
    subject: s?.subject ?? '',
    requesterName: s?.requesterName ?? '',
    requesterContact: s?.requesterContact ?? '',
    replyChannel: s?.replyChannel ?? 'Email',
    receivedOn: intake?.suggestedReceivedOn ?? todayIso(),
    dueOn: intake?.suggestedDueOn ?? '',
    caseNumber: intake?.suggestedCaseNumber ?? '',
    assigneeId: userId,
  }
}

export function ReviewStep({ intake, onRestart }: ReviewStepProps) {
  const user = useCurrentUser()
  const navigate = useNavigate()
  const create = useCreateRequest()
  const { data: users = [] } = useUsers()
  const { data: departments = [] } = useDepartments()

  const [values, setValues] = useState(() => initialValues(intake, user.id))
  const [content, setContent] = useState(intake?.text ?? '')
  const [departmentIds, setDepartmentIds] = useState<string[]>([])
  const [dueTouched, setDueTouched] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})

  const preview = useQuery(deadlinePreviewQuery(values.receivedOn))
  const dueOn = dueTouched ? values.dueOn : (preview.data?.dueOn ?? values.dueOn)

  const aiFields = useMemo(() => {
    const fields = new Set<RequestFormField>()
    if (!intake?.aiUsed) return fields
    const s = intake.suggestion
    if (s.subject) fields.add('subject')
    if (s.requesterName) fields.add('requesterName')
    if (s.requesterContact) fields.add('requesterContact')
    if (s.replyChannel) fields.add('replyChannel')
    return fields
  }, [intake])

  const change = (changes: Partial<RequestFormValues>) => {
    if ('dueOn' in changes) setDueTouched(true)
    setValues((v) => ({ ...v, ...changes }))
  }

  const toggleDepartment = (id: string, checked: boolean) =>
    setDepartmentIds((ids) => (checked ? [...new Set([...ids, id])] : ids.filter((x) => x !== id)))

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!values.subject.trim()) {
      setErrors({ subject: 'Podaj przedmiot wniosku.' })
      return
    }
    create.mutate(
      {
        subject: values.subject.trim(),
        content: content.trim() || null,
        requesterName: values.requesterName.trim() || null,
        requesterContact: values.requesterContact.trim() || null,
        replyChannel: values.replyChannel,
        receivedOn: values.receivedOn,
        dueOn: dueOn || null,
        caseNumber: values.caseNumber.trim() || null,
        assigneeId: values.assigneeId || null,
        sourceFileId: intake?.fileId ?? null,
        departmentIds,
      },
      {
        onSuccess: ({ id }) => {
          notifySuccess('Wniosek zarejestrowany', values.caseNumber || undefined)
          navigate(`/wnioski/${id}`)
        },
        onError: (error) =>
          setErrors(error instanceof ApiError ? { ...error.fieldErrors, form: error.message } : { form: 'Nie udało się zapisać wniosku.' }),
      },
    )
  }

  const suggestion = intake?.suggestion
  const suggestedIds = new Set(intake?.departmentSuggestions.map((s) => s.departmentId))
  const otherDepartments = departments.filter((d) => !suggestedIds.has(d.id))
  const selectedOthers = otherDepartments.filter((d) => departmentIds.includes(d.id))

  return (
    <form onSubmit={submit} noValidate>
      <Grid templateColumns={{ base: '1fr', xl: 'minmax(0, 1fr) 400px' }} gap="5" alignItems="start">
        <Stack gap="5">
          {intake && (intake.warnings.length > 0 || (suggestion && !suggestion.looksLikePublicInformationRequest) || suggestion?.remarks) && (
            <Alert.Root status="warning" variant="outline">
              <Alert.Indicator />
              <Alert.Content>
                <Alert.Title>Zwróć uwagę</Alert.Title>
                <Alert.Description>
                  <List.Root ps="4" mt="1">
                    {intake.warnings.map((w) => (
                      <List.Item key={w}>{w}</List.Item>
                    ))}
                    {suggestion && !suggestion.looksLikePublicInformationRequest && (
                      <List.Item>Pismo może nie być wnioskiem o udostępnienie informacji publicznej — zweryfikuj tryb.</List.Item>
                    )}
                    {suggestion?.remarks && <List.Item>{suggestion.remarks}</List.Item>}
                  </List.Root>
                </Alert.Description>
              </Alert.Content>
            </Alert.Root>
          )}
          {errors.form && (
            <Alert.Root status="error" variant="outline" role="alert">
              <Alert.Indicator />
              <Alert.Title>{errors.form}</Alert.Title>
            </Alert.Root>
          )}

          <Panel
            title="Dane wniosku"
            description="Sprawdź i w razie potrzeby popraw dane przed rejestracją."
            actions={
              intake?.fileName ? (
                <Tag.Root size="lg" variant="outline">
                  <Tag.StartElement>
                    <FileText size={14} aria-hidden />
                  </Tag.StartElement>
                  <Tag.Label>{intake.fileName}</Tag.Label>
                </Tag.Root>
              ) : undefined
            }
          >
            <RequestFormFields
              values={{ ...values, dueOn }}
              onChange={change}
              errors={errors}
              users={users}
              aiFields={aiFields}
              hints={{
                receivedOn: suggestion?.requestDate
                  ? `Data na piśmie: ${formatDate(suggestion.requestDate)}. Termin liczy się od dnia wpływu do urzędu.`
                  : 'Termin liczy się od dnia wpływu do urzędu.',
                dueOn: dueTouched
                  ? 'Termin zmieniony ręcznie.'
                  : '14 dni od wpływu (art. 13 ust. 1 UDIP), przesunięte na dzień roboczy (art. 57 § 4 KPA).',
                caseNumber: 'Nadawany automatycznie — możesz go zmienić.',
              }}
            />
          </Panel>

          <Panel title="Treść wniosku" description="Tekst odczytany z dokumentu — będzie podstawą wyszukiwania i projektu odpowiedzi.">
            <Collapsible.Root defaultOpen={!intake?.fileId}>
              <Collapsible.Trigger asChild>
                <Button variant="outline" size="sm" mb="3">
                  <ChevronDown size={16} aria-hidden /> Pokaż / ukryj treść
                </Button>
              </Collapsible.Trigger>
              <Collapsible.Content>
                <Field.Root>
                  <Field.Label srOnly>Treść wniosku</Field.Label>
                  <Textarea rows={12} value={content} onChange={(e) => setContent(e.target.value)} fontSize="sm" lineHeight="1.6" />
                </Field.Root>
              </Collapsible.Content>
            </Collapsible.Root>
            {suggestion && suggestion.requestedItems.length > 0 && (
              <Box mt="4">
                <Text fontWeight="semibold" mb="1">
                  Wnioskodawca prosi o:
                </Text>
                <List.Root ps="5" gap="1" color="fg.muted">
                  {suggestion.requestedItems.map((item) => (
                    <List.Item key={item}>{item}</List.Item>
                  ))}
                </List.Root>
              </Box>
            )}
          </Panel>

          <Flex gap="3" justify="flex-end" wrap="wrap" position="sticky" bottom="0" bg="bg.canvas" py="3" zIndex="1">
            <Button variant="outline" onClick={onRestart}>
              Zacznij od nowa
            </Button>
            <Button type="submit" size="lg" loading={create.isPending}>
              Zarejestruj wniosek
            </Button>
          </Flex>
        </Stack>

        <Stack gap="5" position={{ xl: 'sticky' }} top={{ xl: '20' }}>
          <Panel
            title="Wydziały do zapytania"
            description="Zaznacz komórki, które przygotują informacje."
            actions={
              intake?.aiUsed ? (
                <Badge colorPalette="purple" variant="subtle" gap="1">
                  <Sparkles size={12} aria-hidden /> AI
                </Badge>
              ) : undefined
            }
          >
            <Stack gap="3">
              {intake && intake.departmentSuggestions.length > 0 ? (
                <DepartmentSuggestionList suggestions={intake.departmentSuggestions} selected={departmentIds} onToggle={toggleDepartment} />
              ) : (
                <Text color="fg.muted" fontSize="sm">
                  Brak sugestii — wybierz wydział z listy.
                </Text>
              )}
              {selectedOthers.length > 0 && (
                <Flex gap="2" wrap="wrap">
                  {selectedOthers.map((d) => (
                    <Tag.Root key={d.id} variant="subtle" size="lg">
                      <Tag.Label>
                        {d.symbol} — {d.name}
                      </Tag.Label>
                      <Tag.EndElement>
                        <Tag.CloseTrigger aria-label={`Usuń ${d.name}`} onClick={() => toggleDepartment(d.id, false)} />
                      </Tag.EndElement>
                    </Tag.Root>
                  ))}
                </Flex>
              )}
              <Field.Root>
                <Field.Label fontSize="sm">Dodaj inny wydział lub biuro</Field.Label>
                <NativeSelect.Root size="sm">
                  <NativeSelect.Field
                    value=""
                    onChange={(e) => e.target.value && toggleDepartment(e.target.value, true)}
                  >
                    <option value="">Wybierz…</option>
                    {otherDepartments.map((d) => (
                      <option key={d.id} value={d.id}>
                        {d.symbol} — {d.name}
                      </option>
                    ))}
                  </NativeSelect.Field>
                  <NativeSelect.Indicator />
                </NativeSelect.Root>
              </Field.Root>
            </Stack>
          </Panel>

          <Panel title="Podobne wnioski" description="Najbardziej zbliżone sprawy z rejestru i archiwum odpowiedzi.">
            {intake && intake.similarCases.length > 0 ? (
              <SimilarCasesList hits={intake.similarCases} newTab />
            ) : (
              <Text color="fg.muted" fontSize="sm">
                Nie znaleziono podobnych spraw.
              </Text>
            )}
          </Panel>
        </Stack>
      </Grid>
    </form>
  )
}
