import type { BindingProblem } from './validation'

/** Shown in place of a widget whose bindings are invalid, instead of rendering wrong content. */
export function WidgetConfigError({ widgetId, problems }: { widgetId: string; problems: BindingProblem[] }) {
  return (
    <div className="widget-config-error" role="alert" data-widget-error={widgetId}>
      <strong className="widget-config-error__title">“{widgetId}” is not configured correctly</strong>
      <ul className="widget-config-error__list">
        {problems.map((p) => (
          <li key={`${p.slot}:${p.message}`}>{p.message}</li>
        ))}
      </ul>
    </div>
  )
}
