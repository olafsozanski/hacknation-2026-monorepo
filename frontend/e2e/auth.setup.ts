import { test as setup } from '@playwright/test'
import { adminStorageState } from '../playwright.config'
import { login } from './support'

setup('sign in as administrator', async ({ page }) => {
  await login(page)
  await page.context().storageState({ path: adminStorageState })
})
