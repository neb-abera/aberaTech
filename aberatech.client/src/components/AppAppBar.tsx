import * as React from 'react';
import {styled, alpha} from '@mui/material/styles';
import Box from '@mui/material/Box';
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Container from '@mui/material/Container';
import Divider from '@mui/material/Divider';
import MenuItem from '@mui/material/MenuItem';
import Drawer from '@mui/material/Drawer';
import MenuIcon from '@mui/icons-material/Menu';
import CloseRoundedIcon from '@mui/icons-material/CloseRounded';
import FingerprintIcon from '@mui/icons-material/Fingerprint';
import ColorModeIconDropdown from '../theme/ColorModeIconDropdown';
import {Link} from 'react-router';

const StyledToolbar = styled(Toolbar)(({theme}) => ({
  display        : 'flex',
  alignItems     : 'center',
  justifyContent : 'space-between',
  flexShrink     : 0,
  borderRadius   : `calc(${theme.shape.borderRadius}px + 8px)`,
  backdropFilter : 'blur(24px)',
  border         : '1px solid',
  borderColor    : theme.palette.divider,
  backgroundColor: alpha(theme.palette.background.default, 0.4),
  boxShadow      : theme.shadows[1],
  padding        : '8px 12px'
}));

export default function AppAppBar()
{
  const [open, setOpen] = React.useState(false);

  const toggleDrawer = (newOpen: boolean) => () =>
  {
    setOpen(newOpen);
  };

  return (
    <AppBar
      position="fixed"
      enableColorOnDark
      sx={{
        boxShadow      : 0,
        bgcolor        : 'transparent',
        backgroundImage: 'none',
        mt             : 'calc(var(--template-frame-height, 0px) + 28px)'
      }}
    >
      <Container maxWidth="lg">
        <StyledToolbar variant="dense" disableGutters>
          <Box sx={{flexGrow: 1, display: 'flex', alignItems: 'center', px: 0}}>
            <FingerprintIcon/>
            <Box sx={{display: {xs: 'none', md: 'flex'}}}>
              <Button variant="text" color="info" size="small">
                <Link to="/" style={{color: 'inherit', textDecoration: 'inherit'}}>
                  Home
                </Link>
              </Button>
              <Button variant="text" color="info" size="small">
                <Link to="/transition" style={{color: 'inherit', textDecoration: 'inherit'}}>
                  {' '}
                  Military Transition Guide{' '}
                </Link>
              </Button>
              <Button variant="text" color="info" size="small">
                <Link to="/technical" style={{color: 'inherit', textDecoration: 'inherit'}}>
                  {' '}
                  Learning Software Development{' '}
                </Link>
              </Button>
            </Box>
          </Box>
          <Box
            sx={{
              display   : {xs: 'none', md: 'flex'},
              gap       : 1,
              alignItems: 'center'
            }}
          >
            <ColorModeIconDropdown/>
          </Box>
          <Box sx={{display: {xs: 'flex', md: 'none'}, gap: 1}}>
            <ColorModeIconDropdown size="medium"/>
            <IconButton aria-label="Menu button" onClick={toggleDrawer(true)}>
              <MenuIcon/>
            </IconButton>
            <Drawer
              anchor="top"
              open={open}
              onClose={toggleDrawer(false)}
              PaperProps={{
                sx: {
                  top: 'var(--template-frame-height, 0px)'
                }
              }}
            >
              <Box sx={{p: 2, backgroundColor: 'background.default'}}>
                <Box
                  sx={{
                    display       : 'flex',
                    justifyContent: 'flex-end'
                  }}
                >
                  <IconButton onClick={toggleDrawer(false)}>
                    <CloseRoundedIcon/>
                  </IconButton>
                </Box>
                {/*nebdebug todo: links broken*/}
                <MenuItem>
                  <Link to="/" style={{color: 'inherit', textDecoration: 'inherit'}}>
                    Home
                  </Link>
                </MenuItem>
                <MenuItem>
                  {' '}
                  <Link to="/transition" style={{color: 'inherit', textDecoration: 'inherit'}}>
                    Military Transition Guide
                  </Link>
                </MenuItem>
                <MenuItem>
                  {' '}
                  <Link to="/technical" style={{color: 'inherit', textDecoration: 'inherit'}}>
                    Technical Transition Guide
                  </Link>
                </MenuItem>
                <Divider sx={{my: 3}}/>
              </Box>
            </Drawer>
          </Box>
        </StyledToolbar>
      </Container>
    </AppBar>
  );
}
