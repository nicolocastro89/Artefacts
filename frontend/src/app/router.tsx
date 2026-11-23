import { createBrowserRouter, Navigate } from 'react-router'
import { artefactEditPage, artefactNewPage } from '../pages/definitions/artefactEdit'
import { CustomPageRoute, PageNotFound, PageRoute } from '../pages/PageRoute'
import { ImportsPage } from '../pages/imports/ImportsPage'
import { ImportWizard } from '../pages/imports/ImportWizard'
import { LanguagesPage } from '../pages/LanguagesPage'
import { UnitsPage } from '../pages/UnitsPage'
import { ListPage } from '../pages/ListPage'
import { ObjectTypesPage } from '../pages/ObjectTypesPage'
import { PageEditor } from '../pages/PageEditor'
import { PagesPage } from '../pages/PagesPage'
import { PartiesPage } from '../pages/PartiesPage'
import { RegionsPage } from '../pages/RegionsPage'
import { PrintPreview } from '../print/PrintPreview'
import { AppShell } from './AppShell'

export const routes = [
  // Print preview has no app chrome: it is the paper.
  { path: '/artefacts/:id/print', element: <PrintPreview /> },
  {
    path: '/',
    element: <AppShell />,
    children: [
      { index: true, element: <Navigate to="/artefacts" replace /> },
      { path: 'artefacts', element: <PageRoute slug="artefact-list" /> },
      { path: 'artefacts/new', element: <PageRoute page={artefactNewPage} /> },
      { path: 'artefacts/:id', element: <PageRoute key="detail" slug="artefact-detail" /> },
      { path: 'artefacts/:id/edit', element: <PageRoute key="edit" page={artefactEditPage} /> },
      { path: 'p/:slug', element: <CustomPageRoute /> },
      // The query builder loads on demand: it brings the GraphQL parser with it.
      { path: 'queries', lazy: async () => ({ Component: (await import('../queries/QueriesPage')).QueriesPage }) },
      { path: 'queries/new', lazy: async () => ({ Component: (await import('../queries/QueryEditor')).QueryEditor }) },
      { path: 'queries/:id', lazy: async () => ({ Component: (await import('../queries/QueryEditor')).QueryEditor }) },
      { path: 'pages', element: <PagesPage /> },
      { path: 'pages/new', element: <PageEditor /> },
      { path: 'pages/:id', element: <PageEditor /> },
      { path: 'reference/regions', element: <RegionsPage /> },
      { path: 'reference/object-types', element: <ObjectTypesPage /> },
      { path: 'reference/dynasties', element: <ListPage key="dynasties" list="dynasties" /> },
      { path: 'reference/collection-groups', element: <ListPage key="collection-groups" list="collection-groups" /> },
      { path: 'reference/storage-locations', element: <ListPage key="storage-locations" list="storage-locations" /> },
      { path: 'reference/parties', element: <PartiesPage /> },
      { path: 'reference/units', element: <UnitsPage /> },
      { path: 'reference/languages', element: <LanguagesPage /> },
      { path: 'imports', element: <ImportsPage /> },
      { path: 'imports/:id', element: <ImportWizard /> },
      { path: '*', element: <PageNotFound /> },
    ],
  },
]

export const router = createBrowserRouter(routes)
