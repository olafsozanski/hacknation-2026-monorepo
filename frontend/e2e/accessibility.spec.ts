import { expect, test } from '@playwright/test'
import { expectNoAccessibilityViolations } from './support'

test.describe('signed out', () => {
  test.use({ storageState: { cookies: [], origins: [] } })

  test('login page meets WCAG 2.1 AA', async ({ page }) => {
    await page.goto('/logowanie')
    await expectNoAccessibilityViolations(page)
  })
})

test('skip link moves focus to main content', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  await page.keyboard.press('Tab')
  await expect(page.getByRole('link', { name: 'Przejdź do treści głównej' })).toBeFocused()
  await page.keyboard.press('Enter')
  await expect(page.locator('#main-content')).toBeFocused()
})

const pages = [
  { path: '/', heading: 'Rejestr wniosków' },
  { path: '/?widok=kalendarz', heading: 'Rejestr wniosków' },
  { path: '/wnioski/nowy', heading: 'Rejestracja wniosku' },
  { path: '/archiwum?q=drzewa', heading: 'Archiwum spraw' },
  { path: '/anonimizacja', heading: 'Anonimizacja dokumentu' },
  { path: '/baza-wiedzy', heading: 'Baza wiedzy' },
  { path: '/wydzialy', heading: 'Wydziały i biura' },
  { path: '/administracja/uzytkownicy', heading: 'Użytkownicy' },
  { path: '/dostepnosc', heading: 'Deklaracja dostępności' },
]

for (const { path, heading } of pages) {
  test(`${path} meets WCAG 2.1 AA`, async ({ page }) => {
    await page.goto(path)
    await expect(page.getByRole('heading', { level: 1, name: heading })).toBeVisible()
    await page.waitForLoadState('networkidle')
    await expectNoAccessibilityViolations(page)
  })
}

test('high contrast mode meets WCAG 2.1 AA', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('button', { name: 'Wersja kontrastowa' }).click()
  await page.waitForLoadState('networkidle')
  await expectNoAccessibilityViolations(page)
})
