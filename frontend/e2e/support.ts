import AxeBuilder from '@axe-core/playwright'
import { expect, type Page } from '@playwright/test'

export const adminUser = { userName: 'admin', password: process.env.E2E_ADMIN_PASSWORD ?? 'Admin12345678' }

export async function login(page: Page, user = adminUser) {
  await page.goto('/logowanie')
  await page.getByLabel('Nazwa użytkownika').fill(user.userName)
  await page.getByLabel('Hasło').fill(user.password)
  await page.getByRole('button', { name: 'Zaloguj się' }).click()
  await expect(page).not.toHaveURL(/logowanie/)
}

export async function expectNoAccessibilityViolations(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze()
  const summary = results.violations.map((v) => `${v.id} (${v.impact}): ${v.nodes.length}× ${v.help}\n    ${v.nodes[0]?.target.join(' ')}`)
  expect(summary, summary.join('\n')).toEqual([])
}
