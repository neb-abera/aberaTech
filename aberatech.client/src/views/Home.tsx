import AppAppBar from '../components/AppAppBar';
import Hero from '../components/Hero';
import CssBaseline from '@mui/material/CssBaseline';
import AppTheme from '../theme/AppTheme';

export default function Home(props: { disableCustomTheme?: boolean }) {
  return (
    <AppTheme {...props}>
      <CssBaseline enableColorScheme />
      <AppAppBar />
      <Hero />
    </AppTheme>
  );
}
