import * as React from 'react';
import DarkModeIcon from '@mui/icons-material/DarkModeRounded';
import LightModeIcon from '@mui/icons-material/LightModeRounded';
import Box from '@mui/material/Box';
import IconButton, { IconButtonOwnProps } from '@mui/material/IconButton';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import { useColorScheme } from '@mui/material/styles';
import { useAccount } from '../hooks/useAccount';
import { availableModes, correctedMode } from './colorMode';

export default function ColorModeIconDropdown(props: IconButtonOwnProps) {
  const { mode, systemMode, setMode } = useColorScheme();

  // "System" is offered to signed-in accounts only. For everybody else the
  // choice is the plain one — dark, or light — and following the operating
  // system is a preference the site does not need to expose to a visitor who
  // has no way to store anything else about themselves.
  const { signedIn, resolved } = useAccount();

  React.useEffect(() => {
    // Not before the probe answers. The initial signedIn is a placeholder
    // "no", and correcting against it rewrote an account holder's stored
    // System preference to dark on every load — visible, on prerendered
    // pages, as a flash of the system scheme before the stomp.
    if (!resolved) {
      return;
    }
    const corrected = correctedMode(mode, signedIn);
    if (corrected) {
      setMode(corrected);
    }
  }, [resolved, signedIn, mode, setMode]);
  const [anchorEl, setAnchorEl] = React.useState<null | HTMLElement>(null);
  const open = Boolean(anchorEl);
  const handleClick = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget);
  };
  const handleClose = () => {
    setAnchorEl(null);
  };
  const handleMode = (targetMode: 'system' | 'light' | 'dark') => () => {
    setMode(targetMode);
    handleClose();
  };
  if (!mode) {
    return (
      <Box
        data-screenshot="toggle-mode"
        sx={(theme) => ({
          verticalAlign: 'bottom',
          display: 'inline-flex',
          width: '2.25rem',
          height: '2.25rem',
          borderRadius: theme.shape.borderRadius,
          border: '1px solid',
          borderColor: theme.palette.divider
        })}
      />
    );
  }
  const resolvedMode = (systemMode || mode) as 'light' | 'dark';
  const icon = {
    light: <LightModeIcon />,
    dark: <DarkModeIcon />
  }[resolvedMode];
  return (
    <React.Fragment>
      <IconButton
        data-screenshot="toggle-mode"
        onClick={handleClick}
        disableRipple
        size="small"
        aria-controls={open ? 'color-scheme-menu' : undefined}
        aria-haspopup="true"
        aria-expanded={open ? 'true' : undefined}
        {...props}
      >
        {icon}
      </IconButton>
      <Menu
        anchorEl={anchorEl}
        id="account-menu"
        open={open}
        onClose={handleClose}
        onClick={handleClose}
        slotProps={{
          paper: {
            variant: 'outlined',
            elevation: 0,
            sx: {
              my: '4px'
            }
          }
        }}
        transformOrigin={{ horizontal: 'right', vertical: 'top' }}
        anchorOrigin={{ horizontal: 'right', vertical: 'bottom' }}
      >
        {availableModes(signedIn).includes('system') ? (
          <MenuItem selected={mode === 'system'} onClick={handleMode('system')}>
            System
          </MenuItem>
        ) : null}
        <MenuItem selected={mode === 'light'} onClick={handleMode('light')}>
          Light
        </MenuItem>
        <MenuItem selected={mode === 'dark'} onClick={handleMode('dark')}>
          Dark
        </MenuItem>
      </Menu>
    </React.Fragment>
  );
}
