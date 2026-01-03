import { ConfigProvider, theme, App as AntApp } from 'antd';
import { Dashboard } from './components';
import './App.css';

function App() {
  return (
    <ConfigProvider
      theme={{
        algorithm: theme.darkAlgorithm,
        token: {
          colorPrimary: '#1890ff',
          borderRadius: 6,
        },
      }}
    >
      <AntApp>
        <Dashboard />
      </AntApp>
    </ConfigProvider>
  );
}

export default App;
