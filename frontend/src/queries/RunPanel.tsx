import { Play } from 'lucide-react'
import { useId, useState } from 'react'
import type { QueryDescription, QueryProblem } from '../api/queries'
import { coerceVariables } from '../widgets/core/dataSources'
import { position, runDocument, type RunResult } from './run'

interface Props {
  document: string
  /** The description of the document, when it is valid. */
  description: QueryDescription | null
  /** What is wrong with the document, when it is not valid. */
  problems: QueryProblem[]
}

/** Runs the unsaved document with values for its variables, and shows what it returns and the paths a page can bind. */
export function RunPanel({ document, description, problems }: Props) {
  const idPrefix = useId()
  const [values, setValues] = useState<Record<string, string>>({})
  const [result, setResult] = useState<RunResult | null>(null)
  const [running, setRunning] = useState(false)
  const [missing, setMissing] = useState<string | null>(null)

  const variables = description?.variables ?? {}
  const names = Object.keys(variables)

  const run = async () => {
    const required = names.find((name) => variables[name]?.required && (values[name] ?? '').trim() === '')
    if (required) {
      setMissing(`Enter a value for $${required}.`)
      return
    }
    setMissing(null)
    setRunning(true)
    try {
      const given = Object.fromEntries(Object.entries(values).filter(([name, text]) => name in variables && text.trim() !== ''))
      setResult(await runDocument(document, coerceVariables(given, variables as Parameters<typeof coerceVariables>[1])))
    } catch (error) {
      setResult({ data: null, errors: [{ message: error instanceof Error ? error.message : 'The query could not be run.', line: null, column: null }] })
    } finally {
      setRunning(false)
    }
  }

  return (
    <section className="editor__run" aria-labelledby={`${idPrefix}-title`}>
      <h2 id={`${idPrefix}-title`}>Run</h2>

      {problems.length > 0 && (
        <ul className="pages__problems" aria-label="Problems found in the query">
          {problems.map((problem, i) => (
            <li key={i} role="alert">
              {problem.message}
              {position(problem)}
            </li>
          ))}
        </ul>
      )}

      {names.map((name) => (
        <div key={name} className="field">
          <label className="field__label" htmlFor={`${idPrefix}-${name}`}>
            ${name} <span className="muted">({variables[name]?.type}
            {variables[name]?.required ? ', required' : ''})</span>
          </label>
          <input id={`${idPrefix}-${name}`} className="input" value={values[name] ?? ''} onChange={(e) => setValues({ ...values, [name]: e.target.value })} />
        </div>
      ))}
      {missing && (
        <p className="field__error" role="alert">
          {missing}
        </p>
      )}

      <div>
        <button type="button" className="btn btn--primary" disabled={running || problems.length > 0 || !description} onClick={() => void run()}>
          <Play aria-hidden="true" /> Run
        </button>
      </div>

      {result && result.errors.length > 0 && (
        <ul className="pages__problems" aria-label="Errors from the server">
          {result.errors.map((error, i) => (
            <li key={i} role="alert">
              {error.message}
              {position(error)}
            </li>
          ))}
        </ul>
      )}
      {result && result.errors.length === 0 && (
        <pre className="plate editor__result" aria-label="Result">
          {JSON.stringify(result.data, null, 2)}
        </pre>
      )}

      {description && (
        <details>
          <summary>Result paths ({Object.keys(description.fields).length})</summary>
          <table className="table">
            <caption className="visually-hidden">Result paths and their types</caption>
            <thead>
              <tr>
                <th scope="col">Path</th>
                <th scope="col">Type</th>
              </tr>
            </thead>
            <tbody>
              {Object.entries(description.fields).map(([path, type]) => (
                <tr key={path}>
                  <td>
                    <code>{path}</code>
                  </td>
                  <td>{type}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </details>
      )}
    </section>
  )
}
