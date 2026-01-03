import { ConfigProvider, theme } from 'antd';
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
      <Dashboard />
    </ConfigProvider>
  );
}

export default App;
