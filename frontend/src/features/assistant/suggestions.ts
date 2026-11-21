const casePattern = /^\/wnioski\/(?!nowy\/?$)[^/]+(\/odpowiedz)?\/?$/

const general = ['Gdzie zarejestruję nowy wniosek?', 'Jak liczony jest termin odpowiedzi?']

function questionsForPage(pathname: string): string[] {
  const caseMatch = casePattern.exec(pathname)
  if (caseMatch?.[1]) return ['Na co zwrócić uwagę przed wysłaniem odpowiedzi?', 'Co mam teraz zrobić w tej sprawie?']
  if (caseMatch) return ['Co mam teraz zrobić w tej sprawie?', 'Do kiedy mogę przedłużyć termin?']
  if (pathname === '/wnioski/nowy') return ['Dlaczego nie mogę zapisać formularza?', 'Jak liczony jest termin odpowiedzi?']
  if (pathname === '/') return ['Które sprawy wymagają dziś mojej uwagi?']
  if (pathname === '/anonimizacja') return ['Jakie dane trzeba zanonimizować w odpowiedzi?']
  if (pathname === '/archiwum') return ['Jak znaleźć podobne sprawy z poprzednich lat?']
  return []
}

export function suggestedQuestions(path: string, hasFieldErrors: boolean): string[] {
  const pathname = path.split(/[?#]/)[0] || '/'
  const questions = [
    ...(hasFieldErrors ? ['Dlaczego nie mogę zapisać formularza?'] : []),
    ...questionsForPage(pathname),
    ...(pathname === '/wnioski/nowy' ? [] : general),
  ]
  return [...new Set(questions)].slice(0, 4)
}
