import { Badge, Field, Grid, GridItem, Input, NativeSelect, Textarea } from '@chakra-ui/react'
import type { ReactNode } from 'react'
import type { ReplyChannel, UserSummary } from '@/api/types'
import { DateInput } from '@/components/common/DateInput'
import { FieldErrorText } from '@/components/common/FieldErrorText'
import { todayIso } from '@/lib/dates'
import { replyChannelLabels } from '@/lib/labels'

export type RequestFormValues = {
  subject: string
  requesterName: string
  requesterContact: string
  replyChannel: ReplyChannel
  receivedOn: string
  dueOn: string
  caseNumber: string
  assigneeId: string
}

export type RequestFormField = keyof RequestFormValues

type Props = {
  values: RequestFormValues
  onChange: (changes: Partial<RequestFormValues>) => void
  errors: Record<string, string>
  users: UserSummary[]
  aiFields?: ReadonlySet<RequestFormField>
  hints?: Partial<Record<RequestFormField, ReactNode>>
  portalled?: boolean
}

function AiMark() {
  return (
    <Badge size="xs" colorPalette="purple" variant="subtle" ms="1.5" title="Wartość zaproponowana przez asystenta — sprawdź ją">
      propozycja AI
    </Badge>
  )
}

export function RequestFormFields({ values, onChange, errors, users, aiFields, hints = {}, portalled = true }: Props) {
  const today = todayIso()
  const ai = (field: RequestFormField) => (aiFields?.has(field) ? <AiMark /> : null)
  return (
    <Grid templateColumns={{ base: '1fr', md: 'repeat(2, minmax(0, 1fr))' }} gap="4">
      <GridItem colSpan={{ base: 1, md: 2 }}>
        <Field.Root required invalid={!!errors.subject}>
          <Field.Label>
            Przedmiot wniosku <Field.RequiredIndicator />
            {ai('subject')}
          </Field.Label>
          <Textarea
            rows={2}
            autoresize
            maxLength={500}
            value={values.subject}
            onChange={(e) => onChange({ subject: e.target.value })}
          />
          <Field.HelperText>Krótki opis, którym wniosek będzie oznaczony w rejestrze.</Field.HelperText>
          <FieldErrorText>{errors.subject}</FieldErrorText>
        </Field.Root>
      </GridItem>
      <Field.Root required invalid={!!errors.receivedOn}>
        <Field.Label>
          Data wpływu <Field.RequiredIndicator />
        </Field.Label>
        <DateInput value={values.receivedOn} max={today} onChange={(receivedOn) => onChange({ receivedOn })} portalled={portalled} />
        {hints.receivedOn && <Field.HelperText>{hints.receivedOn}</Field.HelperText>}
        <FieldErrorText>{errors.receivedOn}</FieldErrorText>
      </Field.Root>
      <Field.Root required invalid={!!errors.dueOn}>
        <Field.Label>
          Termin odpowiedzi <Field.RequiredIndicator />
        </Field.Label>
        <DateInput value={values.dueOn} min={values.receivedOn || undefined} onChange={(dueOn) => onChange({ dueOn })} portalled={portalled} />
        {hints.dueOn && <Field.HelperText>{hints.dueOn}</Field.HelperText>}
        <FieldErrorText>{errors.dueOn}</FieldErrorText>
      </Field.Root>
      <Field.Root invalid={!!errors.caseNumber}>
        <Field.Label>Znak sprawy</Field.Label>
        <Input value={values.caseNumber} maxLength={64} onChange={(e) => onChange({ caseNumber: e.target.value })} />
        {hints.caseNumber && <Field.HelperText>{hints.caseNumber}</Field.HelperText>}
        <FieldErrorText>{errors.caseNumber}</FieldErrorText>
      </Field.Root>
      <Field.Root invalid={!!errors.assigneeId}>
        <Field.Label>Prowadzący sprawę</Field.Label>
        <NativeSelect.Root>
          <NativeSelect.Field value={values.assigneeId} onChange={(e) => onChange({ assigneeId: e.target.value })}>
            <option value="">— nieprzypisana —</option>
            {users.map((user) => (
              <option key={user.id} value={user.id}>
                {user.fullName} ({user.initials})
              </option>
            ))}
          </NativeSelect.Field>
          <NativeSelect.Indicator />
        </NativeSelect.Root>
        <FieldErrorText>{errors.assigneeId}</FieldErrorText>
      </Field.Root>
      <Field.Root invalid={!!errors.requesterName}>
        <Field.Label>
          Wnioskodawca
          {ai('requesterName')}
        </Field.Label>
        <Input
          value={values.requesterName}
          maxLength={300}
          autoComplete="off"
          onChange={(e) => onChange({ requesterName: e.target.value })}
        />
        <FieldErrorText>{errors.requesterName}</FieldErrorText>
      </Field.Root>
      <Field.Root invalid={!!errors.requesterContact}>
        <Field.Label>
          Adres do korespondencji
          {ai('requesterContact')}
        </Field.Label>
        <Input
          value={values.requesterContact}
          maxLength={500}
          autoComplete="off"
          placeholder="E-mail, adres pocztowy lub skrytka ePUAP"
          onChange={(e) => onChange({ requesterContact: e.target.value })}
        />
        <FieldErrorText>{errors.requesterContact}</FieldErrorText>
      </Field.Root>
      <Field.Root>
        <Field.Label>
          Forma udostępnienia odpowiedzi
          {ai('replyChannel')}
        </Field.Label>
        <NativeSelect.Root>
          <NativeSelect.Field
            value={values.replyChannel}
            onChange={(e) => onChange({ replyChannel: e.target.value as ReplyChannel })}
          >
            {Object.entries(replyChannelLabels).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </NativeSelect.Field>
          <NativeSelect.Indicator />
        </NativeSelect.Root>
      </Field.Root>
    </Grid>
  )
}
