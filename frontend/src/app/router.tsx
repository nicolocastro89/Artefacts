import { createBrowserRouter, Link, Navigate } from 'react-router'
import { artefactDetailPage } from '../pages/definitions/artefactDetail'
import { artefactEditPage, artefactNewPage } from '../pages/definitions/artefactEdit'
import { artefactListPage } from '../pages/definitions/artefactList'
import { PageRoute } from '../pages/PageRoute'
import { ImportsPage } from '../pages/imports/ImportsPage'
import { ImportWizard } from '../pages/imports/ImportWizard'
import { LanguagesPage } from '../pages/LanguagesPage'
import { ObjectTypesPage } from '../pages/ObjectTypesPage'
import { RegionsPage } from '../pages/RegionsPage'
import { PrintPreview } from '../print/PrintPreview'
import { AppShell } from './AppShell'

function NotFound() {
  return (
    <div className="plate empty">
      <strong>This page does not exist.</strong>
      <Link className="btn" to="/artefacts">
        Go to artefacts
      </Link>
    </div>
  )
}

export const routes = [
  // Print preview has no app chrome: it is the paper.
  { path: '/artefacts/:id/print', element: <PrintPreview /> },
  {
    path: '/',
    element: <AppShell />,
    children: [
      { index: true, element: <Navigate to="/artefacts" replace /> },
      { path: 'artefacts', element: <PageRoute page={artefactListPage} /> },
      { path: 'artefacts/new', element: <PageRoute page={artefactNewPage} /> },
      { path: 'artefacts/:id', element: <PageRoute key="detail" page={artefactDetailPage} /> },
      { path: 'artefacts/:id/edit', element: <PageRoute key="edit" page={artefactEditPage} /> },
      { path: 'reference/regions', element: <RegionsPage /> },
      { path: 'reference/object-types', element: <ObjectTypesPage /> },
      { path: 'reference/languages', element: <LanguagesPage /> },
      { path: 'imports', element: <ImportsPage /> },
      { path: 'imports/:id', element: <ImportWizard /> },
      { path: '*', element: <NotFound /> },
    ],
  },
]

export const router = createBrowserRouter(routes)
