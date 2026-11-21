import { DatePicker, Field, Portal, parseDate } from '@chakra-ui/react'
import { CalendarDays } from 'lucide-react'
import type { ReactNode } from 'react'
import type { IsoDate } from '@/api/types'
import { formatPolishDate, parsePolishDate } from '@/lib/polishDate'

type View = 'day' | 'month' | 'year'

const nextLabels: Record<View, string> = { day: 'Następny miesiąc', month: 'Następny rok', year: 'Następna dekada' }
const prevLabels: Record<View, string> = { day: 'Poprzedni miesiąc', month: 'Poprzedni rok', year: 'Poprzednia dekada' }
const viewLabels: Record<View, string> = { day: 'Wybierz miesiąc', month: 'Wybierz rok', year: 'Wybierz dekadę' }

const translations: DatePicker.RootProps['translations'] = {
  dayCell: (state) => {
    if (state.unavailable || state.disabled) return `Niedostępna data: ${state.valueText}`
    if (state.selected) return `Wybrana data: ${state.valueText}`
    if (state.today) return `Dziś, ${state.valueText}`
    return `Wybierz ${state.valueText}`
  },
  nextTrigger: (view: View) => nextLabels[view],
  prevTrigger: (view: View) => prevLabels[view],
  viewTrigger: (view: View) => viewLabels[view],
  presetTrigger: () => 'Wybierz zakres',
  trigger: (open: boolean) => (open ? 'Zamknij kalendarz' : 'Otwórz kalendarz'),
  content: 'Kalendarz',
  clearTrigger: 'Wyczyść datę',
  monthSelect: 'Miesiąc',
  yearSelect: 'Rok',
  placeholder: () => ({ day: 'dd', month: 'mm', year: 'rrrr' }),
}

function CalendarHeader() {
  return (
    <DatePicker.Context>
      {(picker) => (
        <DatePicker.ViewControl>
          <DatePicker.PrevTrigger />
          <DatePicker.ViewTrigger aria-label={`${picker.visibleRangeText.formatted}. ${viewLabels[picker.view]}`}>
            <DatePicker.RangeText />
          </DatePicker.ViewTrigger>
          <DatePicker.NextTrigger />
        </DatePicker.ViewControl>
      )}
    </DatePicker.Context>
  )
}

type DateInputProps = {
  value: IsoDate | ''
  onChange: (value: IsoDate | '') => void
  min?: IsoDate
  max?: IsoDate
  portalled?: boolean
}

export function DateInput({ value, onChange, min, max, portalled = true }: DateInputProps) {
  const calendar: ReactNode = (
    <DatePicker.Positioner>
      <DatePicker.Content>
        <DatePicker.View view="day">
          <CalendarHeader />
          <DatePicker.DayTable />
        </DatePicker.View>
        <DatePicker.View view="month">
          <CalendarHeader />
          <DatePicker.MonthTable />
        </DatePicker.View>
        <DatePicker.View view="year">
          <CalendarHeader />
          <DatePicker.YearTable />
        </DatePicker.View>
      </DatePicker.Content>
    </DatePicker.Positioner>
  )

  return (
    <Field.Context>
      {(field) => (
        <DatePicker.Root
          ids={{ label: () => field.ids.label, input: () => field.ids.control }}
          invalid={field.invalid}
          required={field.required}
          disabled={field.disabled}
          locale="pl-PL"
          startOfWeek={1}
          format={formatPolishDate}
          parse={parsePolishDate}
          placeholder="dd.mm.rrrr"
          translations={translations}
          value={value ? [parseDate(value)] : []}
          onValueChange={(details) => onChange(details.value[0]?.toString() ?? '')}
          min={min ? parseDate(min) : undefined}
          max={max ? parseDate(max) : undefined}
          positioning={portalled ? undefined : { strategy: 'fixed' }}
          w="full"
        >
          <DatePicker.Control>
            <DatePicker.Input />
            <DatePicker.IndicatorGroup>
              <DatePicker.Trigger>
                <CalendarDays aria-hidden />
              </DatePicker.Trigger>
            </DatePicker.IndicatorGroup>
          </DatePicker.Control>
          {portalled ? <Portal>{calendar}</Portal> : calendar}
        </DatePicker.Root>
      )}
    </Field.Context>
  )
}
