import { useQueryClient } from '@tanstack/react-query'
import { Star, Trash2, Upload } from 'lucide-react'
import { useRef, useState, type DragEvent } from 'react'
import { api, type ArtefactImage } from '../../../api/client'
import { ApiError, unwrap, type Problem } from '../../../api/problem'
import { defineWidget } from '../../core/registry'
import { ImagePlate } from '../display'

const ACCEPT = 'image/jpeg,image/png,image/tiff,image/webp,.jpg,.jpeg,.png,.tif,.tiff,.webp'

/** Uploads with progress (fetch cannot report upload progress). */
function uploadFiles(artefactId: string, files: File[], onProgress: (fraction: number) => void): Promise<void> {
  return new Promise((resolve, reject) => {
    const form = new FormData()
    for (const f of files) form.append('files', f, f.name)
    const xhr = new XMLHttpRequest()
    xhr.open('POST', `/api/artefacts/${encodeURIComponent(artefactId)}/images`)
    xhr.upload.onprogress = (e) => e.lengthComputable && onProgress(e.loaded / e.total)
    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) return resolve()
      let problem: Problem = {}
      try {
        problem = JSON.parse(xhr.responseText) as Problem
      } catch {
        /* non-JSON */
      }
      reject(new ApiError(xhr.status, problem))
    }
    xhr.onerror = () => reject(new Error('The upload was interrupted. Check the connection and try again.'))
    xhr.send(form)
  })
}

function describe(e: unknown): string {
  if (e instanceof ApiError) {
    const messages = Object.values(e.fieldErrors).flat()
    return messages.length > 0 ? messages.join(' ') : e.message
  }
  return e instanceof Error ? e.message : 'Something went wrong.'
}

function size(bytes: number): string {
  return bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`
}

defineWidget({
  id: 'image-manager',
  label: 'Image manager',
  slots: { id: { type: 'reference', required: true }, images: { type: 'list', required: true } },
  config: {},
  print: { hideInPrint: true },
  component: function ImageManager({ values }) {
    const artefactId = String(values.id?.value ?? '')
    const images = (Array.isArray(values.images?.value) ? values.images.value : []) as ArtefactImage[]
    const queryClient = useQueryClient()
    const input = useRef<HTMLInputElement>(null)
    const [progress, setProgress] = useState<number | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [busy, setBusy] = useState<string | null>(null)
    const [dragging, setDragging] = useState(false)

    const refresh = () => Promise.all([queryClient.invalidateQueries({ queryKey: ['operation'] })])

    const upload = async (files: File[]) => {
      if (files.length === 0) return
      setError(null)
      setProgress(0)
      try {
        await uploadFiles(artefactId, files, setProgress)
        await refresh()
      } catch (e) {
        setError(describe(e))
      } finally {
        setProgress(null)
        if (input.current) input.current.value = ''
      }
    }

    const act = async (id: string, run: () => Promise<unknown>) => {
      setBusy(id)
      setError(null)
      try {
        await run()
        await refresh()
      } catch (e) {
        setError(describe(e))
      } finally {
        setBusy(null)
      }
    }

    const path = (mediaAssetId: string) => ({ params: { path: { id: artefactId, mediaAssetId } } })

    const onDrop = (e: DragEvent) => {
      e.preventDefault()
      setDragging(false)
      void upload([...e.dataTransfer.files])
    }

    return (
      <section className="image-manager" aria-label="Images">
        <div
          className={`dropzone${dragging ? ' dropzone--over' : ''}`}
          onDragOver={(e) => {
            e.preventDefault()
            setDragging(true)
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={onDrop}
        >
          <Upload aria-hidden="true" />
          <p>
            Drop images here, or{' '}
            <button type="button" className="btn btn--quiet btn--sm" onClick={() => input.current?.click()} disabled={progress !== null}>
              choose files
            </button>
          </p>
          <p className="field__hint">JPEG, PNG, TIFF or WebP. Originals are kept unchanged.</p>
          <input ref={input} type="file" multiple accept={ACCEPT} className="visually-hidden" aria-label="Upload images" onChange={(e) => void upload([...(e.target.files ?? [])])} />
          {progress !== null && (
            <div className="upload-progress" role="progressbar" aria-label="Uploading" aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(progress * 100)}>
              <span style={{ inlineSize: `${Math.round(progress * 100)}%` }} />
            </div>
          )}
        </div>

        {error && (
          <div className="notice notice--error" role="alert">
            {error}
          </div>
        )}

        {images.length > 0 && (
          <ul className="image-manager__list">
            {images.map((img) => (
              <li key={img.mediaAssetId} className={`image-item${img.isPrimary ? ' image-item--primary' : ''}`} aria-busy={busy === img.mediaAssetId}>
                <ImagePlate urls={img.urls} size="thumb" alt={img.caption ?? img.originalFileName} />
                <div className="image-item__body">
                  <div className="image-item__name">
                    {img.isPrimary && (
                      <span className="image-item__primary">
                        <Star aria-hidden="true" /> Primary
                      </span>
                    )}
                    <span title={img.originalFileName}>{img.originalFileName}</span>
                    <span className="muted">
                      {img.format.toUpperCase()} · {size(img.sizeBytes)}
                    </span>
                  </div>
                  <CaptionEditor
                    value={img.caption ?? ''}
                    onSave={(caption) => act(img.mediaAssetId, async () => unwrap(await api.PATCH('/api/artefacts/{id}/images/{mediaAssetId}', { ...path(img.mediaAssetId), body: { caption } })))}
                  />
                </div>
                <div className="image-item__tools">
                  {!img.isPrimary && (
                    <button type="button" className="btn btn--sm" disabled={busy !== null} onClick={() => act(img.mediaAssetId, async () => unwrap(await api.PUT('/api/artefacts/{id}/images/{mediaAssetId}/primary', path(img.mediaAssetId))))}>
                      <Star aria-hidden="true" /> Make primary
                    </button>
                  )}
                  <button
                    type="button"
                    className="btn btn--sm btn--danger"
                    disabled={busy !== null}
                    onClick={() => {
                      if (window.confirm(`Delete “${img.originalFileName}”? The stored original is removed too.`))
                        void act(img.mediaAssetId, async () => unwrap(await api.DELETE('/api/artefacts/{id}/images/{mediaAssetId}', path(img.mediaAssetId))))
                    }}
                  >
                    <Trash2 aria-hidden="true" /> Delete
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
    )
  },
})

function CaptionEditor({ value, onSave }: { value: string; onSave: (caption: string | null) => Promise<unknown> }) {
  const [text, setText] = useState(value)
  return (
    <label className="field">
      <span className="visually-hidden">Caption</span>
      <input
        className="input"
        placeholder="Add a caption"
        value={text}
        onChange={(e) => setText(e.target.value)}
        onBlur={() => text !== value && void onSave(text.trim() || null)}
        onKeyDown={(e) => e.key === 'Enter' && (e.currentTarget as HTMLInputElement).blur()}
      />
    </label>
  )
}
