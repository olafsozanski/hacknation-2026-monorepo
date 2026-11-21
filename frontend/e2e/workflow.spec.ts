import { expect, test } from '@playwright/test'

const requestText = `Szanowni Państwo,
na podstawie art. 2 ust. 1 ustawy z dnia 6 września 2001 r. o dostępie do informacji publicznej wnoszę o udostępnienie
informacji o liczbie ławek parkowych wymienionych w Bydgoszczy w 2026 r. oraz o łącznym koszcie tych prac.
Odpowiedź proszę przesłać na adres e-mail: jan.testowy@example.com.
Z poważaniem, Jan Testowy`

const replyText = `W 2026 r. Wydział Zieleni i Gospodarki Komunalnej wymienił 48 ławek parkowych.
Łączny koszt prac wyniósł 96 000 zł brutto. Kontakt w sprawie: Jan Testowy, tel. 600 100 200.`

test('clerk registers a request, adds a department reply, prepares and exports an anonymised response', async ({ page }) => {
  await page.goto('/wnioski/nowy')
  await page.getByPlaceholder(/treść wiadomości e-mail/).fill(requestText)
  await page.getByRole('button', { name: 'Analizuj treść' }).click()

  await expect(page.getByRole('heading', { name: 'Dane wniosku' })).toBeVisible({ timeout: 30_000 })
  const subject = page.getByLabel(/Przedmiot wniosku/)
  if (!(await subject.inputValue())) await subject.fill('Wymiana ławek parkowych w 2026 r.')
  await expect(page.getByLabel(/Termin odpowiedzi/)).not.toHaveValue('')
  const suggested = page.getByRole('checkbox', { name: /WGK/ })
  if (await suggested.count()) await suggested.check({ force: true })
  else await page.getByLabel('Dodaj inny wydział lub biuro').selectOption({ label: 'WGK — Wydział Zieleni i Gospodarki Komunalnej' })
  await page.getByRole('button', { name: 'Zarejestruj wniosek' }).click()

  await expect(page).toHaveURL(/\/wnioski\/[0-9a-f-]+$/)
  await expect(page.getByRole('heading', { name: 'Zapytania do wydziałów' })).toBeVisible()
  await page.getByRole('button', { name: 'Oznacz jako wysłane' }).first().click()
  await expect(page.getByText(/Oczekuje/)).toBeVisible()

  const fileChooser = page.waitForEvent('filechooser')
  await page.getByRole('button', { name: 'Dodaj plik' }).click()
  await (await fileChooser).setFiles({ name: 'odpowiedz-wgk.txt', mimeType: 'text/plain', buffer: Buffer.from(replyText, 'utf-8') })
  await expect(page.getByRole('main').getByText('odpowiedz-wgk.txt')).toBeVisible()

  await page.getByRole('button', { name: 'Przygotuj projekt odpowiedzi' }).click()
  await expect(page).toHaveURL(/\/odpowiedz$/, { timeout: 30_000 })
  await expect(page.getByRole('heading', { name: 'Anonimizacja' })).toBeVisible()

  await page.getByRole('button', { name: 'Ukryj wszystkie sugestie' }).click()
  await page.getByRole('button', { name: 'Zapisz', exact: true }).click()
  await expect(page.getByText(/Zapisano o/)).toBeVisible()

  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: /Pobierz/ }).click()
  await page.getByRole('menuitem', { name: /Tekst/ }).click()
  const confirm = page.getByRole('button', { name: 'Pobierz mimo to' })
  await confirm.click({ timeout: 3_000 }).catch(() => undefined)
  const file = await download
  const content = await (await file.createReadStream()).toArray()
  const text = Buffer.concat(content).toString('utf-8')
  expect(text).toContain('[dane zanonimizowane]')
  expect(text).not.toContain('600 100 200')
})

test('standalone anonymisation hides personal data in the exported document', async ({ page }) => {
  await page.goto('/anonimizacja')
  await page
    .getByLabel('Tekst do anonimizacji')
    .fill('Wnioskodawca: Jan Kowalski, PESEL 44051401359, tel. 600 100 200, e-mail jan.kowalski@example.com.')
  await page.getByRole('button', { name: 'Wykryj dane do ukrycia' }).click()
  await expect(page.getByRole('heading', { name: 'Anonimizacja' })).toBeVisible({ timeout: 30_000 })
  await page.getByRole('button', { name: 'Ukryj wszystkie sugestie' }).click()

  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: /Pobierz zanonimizowany/ }).click()
  await page.getByRole('menuitem', { name: /Tekst/ }).click()
  const file = await download
  const text = Buffer.concat(await (await file.createReadStream()).toArray()).toString('utf-8')
  expect(text).toContain('[dane zanonimizowane]')
  expect(text).not.toContain('44051401359')
  expect(text).not.toContain('600 100 200')
  expect(text).not.toContain('jan.kowalski@example.com')
})

test('a sample request finds the matching case from the archive', async ({ page }) => {
  await page.goto('/wnioski/nowy')
  await page
    .getByPlaceholder(/treść wiadomości e-mail/)
    .fill(
      'Wnioskuję o udostępnienie informacji publicznej: edytowalne zestawienie liczby uczniów oraz uczniów cudzoziemców w poszczególnych szkołach podstawowych w Bydgoszczy według stanu na koniec czerwca 2026 oraz na wrzesień 2026.',
    )
  await page.getByRole('button', { name: 'Analizuj treść' }).click()
  await expect(page.getByRole('heading', { name: 'Podobne wnioski' })).toBeVisible({ timeout: 30_000 })
  await expect(page.getByText('BKS.IV.1431.307.2025')).toBeVisible()
})
