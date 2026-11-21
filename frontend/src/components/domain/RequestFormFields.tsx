import { Badge, Field, Grid, GridItem, Input, NativeSelect, Textarea } from '@chakra-ui/react'
import type { ReactNode } from 'react'
import type { ReplyChannel, UserSummary } from '@/api/types'
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
}

function AiMark() {
  return (
    <Badge size="xs" colorPalette="purple" variant="subtle" ms="1.5" title="Wartość zaproponowana przez asystenta — sprawdź ją">
      propozycja AI
    </Badge>
  )
}

export function RequestFormFields({ values, onChange, errors, users, aiFields, hints = {} }: Props) {
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
          <Field.ErrorText>{errors.subject}</Field.ErrorText>
        </Field.Root>
      </GridItem>
      <Field.Root required invalid={!!errors.receivedOn}>
        <Field.Label>
          Data wpływu <Field.RequiredIndicator />
        </Field.Label>
        <Input type="date" value={values.receivedOn} onChange={(e) => onChange({ receivedOn: e.target.value })} />
        {hints.receivedOn && <Field.HelperText>{hints.receivedOn}</Field.HelperText>}
        <Field.ErrorText>{errors.receivedOn}</Field.ErrorText>
      </Field.Root>
      <Field.Root required invalid={!!errors.dueOn}>
        <Field.Label>
          Termin odpowiedzi <Field.RequiredIndicator />
        </Field.Label>
        <Input type="date" value={values.dueOn} onChange={(e) => onChange({ dueOn: e.target.value })} />
        {hints.dueOn && <Field.HelperText>{hints.dueOn}</Field.HelperText>}
        <Field.ErrorText>{errors.dueOn}</Field.ErrorText>
      </Field.Root>
      <Field.Root invalid={!!errors.caseNumber}>
        <Field.Label>Znak sprawy</Field.Label>
        <Input value={values.caseNumber} maxLength={64} onChange={(e) => onChange({ caseNumber: e.target.value })} />
        {hints.caseNumber && <Field.HelperText>{hints.caseNumber}</Field.HelperText>}
        <Field.ErrorText>{errors.caseNumber}</Field.ErrorText>
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
        <Field.ErrorText>{errors.assigneeId}</Field.ErrorText>
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
        <Field.ErrorText>{errors.requesterName}</Field.ErrorText>
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
        <Field.ErrorText>{errors.requesterContact}</Field.ErrorText>
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
