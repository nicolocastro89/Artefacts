import { Plus, Trash2 } from 'lucide-react'
import type { OrderItem } from '../model'

interface Props {
  order: OrderItem[]
  fields: { name: string; label: string }[]
  readOnly?: boolean
  onChange: (order: OrderItem[]) => void
}

/** The fields a list is ordered by, in sequence, each ascending or descending. */
export function OrderBlocks({ order, fields, readOnly, onChange }: Props) {
  const set = (index: number, item: OrderItem | null) => onChange(item ? order.map((o, i) => (i === index ? item : o)) : order.filter((_, i) => i !== index))
  const add = () => {
    const field = fields.find((f) => !order.some((o) => o.field === f.name)) ?? fields[0]
    if (field) onChange([...order, { field: field.name, direction: 'ASC' }])
  }

  return (
    <div className="blocks__order">
      {order.length === 0 && <p className="muted">No extra ordering.</p>}
      <ol className="blocks__items">
        {order.map((item, index) => (
          <li key={index} className="blocks__condition">
            <span className="blocks__sigil">{index === 0 ? 'Order by' : 'then by'}</span>
            <select className="input" aria-label={`Order ${index + 1}: field`} value={item.field} disabled={readOnly} onChange={(e) => set(index, { ...item, field: e.target.value })}>
              {fields.map((f) => (
                <option key={f.name} value={f.name}>
                  {f.label}
                </option>
              ))}
            </select>
            <select className="input" aria-label={`Order ${index + 1}: direction`} value={item.direction} disabled={readOnly} onChange={(e) => set(index, { ...item, direction: e.target.value as 'ASC' | 'DESC' })}>
              <option value="ASC">Ascending</option>
              <option value="DESC">Descending</option>
            </select>
            {!readOnly && (
              <button type="button" className="btn btn--sm btn--danger" aria-label={`Remove order ${index + 1}`} onClick={() => set(index, null)}>
                <Trash2 aria-hidden="true" />
              </button>
            )}
          </li>
        ))}
      </ol>
      {!readOnly && (
        <button type="button" className="btn btn--sm" onClick={add}>
          <Plus aria-hidden="true" /> Order by
        </button>
      )}
    </div>
  )
}
