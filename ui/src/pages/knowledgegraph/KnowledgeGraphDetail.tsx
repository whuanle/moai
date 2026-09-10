import { Link, useParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { Page } from '@/design-system'

export function KnowledgeGraphDetail() {
  const { t } = useTranslation()
  const { teamId } = useParams<{ teamId: string }>()

  return (
    <Page breadcrumb={[{ title: <Link to={`/team/${teamId}`}>{t('knowledgegraph.title')}</Link> }]} />
  )
}
