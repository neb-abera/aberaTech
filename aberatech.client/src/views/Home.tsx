import CssBaseline from "@mui/material/CssBaseline";
import AppAppBar from "../components/AppAppBar";
import Footer from "../components/Footer";
import Hero from "../components/Hero";
import AppTheme from "../theme/AppTheme";

export default function Home(props: { disableCustomTheme?: boolean }) {
  return (
    <AppTheme {...props}>
      <CssBaseline enableColorScheme />
      <AppAppBar />
      <Hero />
      <Footer />
    </AppTheme>
  );
}
