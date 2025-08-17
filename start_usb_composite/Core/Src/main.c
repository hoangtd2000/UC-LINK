/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.c
  * @brief          : Main program body
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2025 STMicroelectronics.
  * All rights reserved.
  *
  * This software is licensed under terms that can be found in the LICENSE file
  * in the root directory of this software component.
  * If no LICENSE file comes with this software, it is provided AS-IS.
  *
  ******************************************************************************
  */
/* USER CODE END Header */
/* Includes ------------------------------------------------------------------*/
#include "main.h"
#include "can.h"
#include "dma.h"
#include "i2c.h"
#include "tim.h"
#include "usart.h"
#include "usb_otg.h"
#include "gpio.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */
#include "usb_device.h"
#include "usbd_hid_custom.h"
#include "usbd_hid_custom_if.h"
#include "math.h"
#include "float.h"
/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */
extern USBD_HandleTypeDef hUsbDevice;
extern CAN_HandleTypeDef hcan1;
extern TIM_HandleTypeDef htim4;
extern TIM_HandleTypeDef htim5;
/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */

/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */

/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/

/* USER CODE BEGIN PV */

CAN_TxHeaderTypeDef TxHeader;
CAN_RxHeaderTypeDef RxHeader;
CAN_FilterTypeDef  	sFilterConfig;
uint32_t txMailbox;
static  uint8_t usbFrame[64] = {0};
extern HID_FrameFIFO_t hid_frame_fifo;
HID_FrameFIFO_t hid_frame_fifo_receive;
uint32_t apb1_freq = 0;

#define SAMPLE_POINT_SCALE 1000U
typedef struct {
    int tq_total;
    int prescaler;
    int tseg1;
    int tseg2;
    float actual_sample_point;
    float sample_point_error;
} CAN_TimingConfig;


/* USER CODE END PV */

/* Private function prototypes -----------------------------------------------*/
void SystemClock_Config(void);
/* USER CODE BEGIN PFP */
void Process_HID_Frames(void);
void CanTx_init(uint32_t id, uint8_t dlc, uint8_t *data);
void CanRx_init(void);
void CanRx_FilterRange(uint32_t start_id, uint32_t end_id, uint8_t is_extended);

uint8_t SendCanConfig(uint8_t *data);
uint8_t SendCanMessage(uint8_t *data);
uint8_t SendCanConfigConnect(uint8_t *data);
uint8_t SendCanConfigDisconnect(uint8_t *data);
uint8_t SendCanConfigBaud(uint8_t *data);
uint8_t SendCanConfigFilter(uint8_t *data);
uint8_t (*FuncSendCanArray[3])(uint8_t *data) = {0,SendCanConfig,SendCanMessage};
CAN_TimingConfig find_best_timing(uint32_t baudrate, uint16_t desired_sample_point);

/* USER CODE END PFP */

/* Private user code ---------------------------------------------------------*/
/* USER CODE BEGIN 0 */
void Process_HID_Frames(void) {
    uint8_t frame[HID_FRAME_SIZE];

    while (HID_Frame_Read(&hid_frame_fifo,frame)) {
    	FuncSendCanArray[frame[0]](frame);
    }
}

uint8_t SendCanConfig(uint8_t *data){
	switch(data[1]){
	case 0 :
		SendCanConfigDisconnect(data);
		HAL_GPIO_TogglePin(GPIOA, GPIO_PIN_6);
		break;
	default:
		SendCanConfigConnect(data);
		HAL_GPIO_TogglePin(GPIOA, GPIO_PIN_7);
		break;
	}
	return 1;
}
uint8_t SendCanConfigConnect(uint8_t *data){
	  HAL_TIM_Base_Start(&htim5);
	  HAL_TIM_Base_Start_IT(&htim4);
	  SendCanConfigBaud(data);
	  SendCanConfigFilter(data);
	  HAL_CAN_Start(&hcan1);

	  //CanRx_init();
	  return 1;
}
uint8_t SendCanConfigDisconnect(uint8_t *data){
	  HAL_TIM_Base_Stop(&htim5);
	  HAL_TIM_Base_Stop_IT(&htim4);
	  if (HAL_CAN_DeInit(&hcan1) != HAL_OK)
	   {
	     Error_Handler();
	   }
	  HAL_CAN_Stop(&hcan1);
	  return 1;

}
uint8_t SendCanConfigBaud(uint8_t *data){
	uint32_t baudrate = ((data[2] << 8) | data[1]) * 1000;

	//uint16_t desired_sample_point = 875;
	uint16_t desired_sample_point = (data[4] << 8) | data[3];
    CAN_TimingConfig config = find_best_timing(baudrate, desired_sample_point);
	hcan1.Instance = CAN1;
	hcan1.Init.Prescaler = config.prescaler;
	hcan1.Init.Mode = CAN_MODE_NORMAL;
	hcan1.Init.SyncJumpWidth = CAN_SJW_1TQ;
	hcan1.Init.TimeSeg1 = (config.tseg1 - 1 ) << 16;
	hcan1.Init.TimeSeg2 = (config.tseg2 - 1 ) << 20;
	hcan1.Init.TimeTriggeredMode = DISABLE;
	hcan1.Init.AutoBusOff = DISABLE;
	hcan1.Init.AutoWakeUp = DISABLE;
	hcan1.Init.AutoRetransmission = DISABLE;
	hcan1.Init.ReceiveFifoLocked = DISABLE;
	hcan1.Init.TransmitFifoPriority = DISABLE;
	if (HAL_CAN_Init(&hcan1) != HAL_OK)
	{
		Error_Handler();
	}
	return 1;
}
uint8_t SendCanConfigFilter(uint8_t *data){
	uint32_t start_id =  (data[9] << 24 ) | (data[8] << 16 ) | (data[7] << 8 ) | data[6];
	uint32_t end_id = (data[13] << 24 ) | (data[12] << 16 ) | (data[11] << 8 ) | data[10];
	CanRx_FilterRange(start_id, end_id, data[5]);
	return 1;
}
/*
 * Function: find_best_timing
 * ---------------------------
 * Tìm cấu hình CAN tốt nhất (prescaler, tseg1, tseg2) để đạt được baudrate mong muốn
 * và sample point gần với giá trị yêu cầu nhất (dưới dạng fixed-point).
 *
 * Input:
 *   - baudrate: Tốc độ truyền CAN mong muốn (bit/s)
 *   - desired_sample_point_scaled: Sample point mong muốn đã nhân với 1000
 *       (ví dụ: 0.875 → truyền vào 875)
 *
 * Output:
 *   - Trả về cấu trúc CAN_TimingConfig với cấu hình tốt nhất đã tìm được
 *
 * Sequence:
 *   [1] Duyệt tổng số Time Quanta (TQ) từ 8 đến 25 (theo chuẩn CAN)
 *   [2] Với mỗi giá trị tq_total:
 *       [2.1] Kiểm tra xem tq_total có chia hết (PCLK1 / baudrate) không
 *             → Nếu không chia hết thì bỏ qua
 *       [2.2] Tính prescaler: prescaler = (PCLK1 / baudrate) / tq_total
 *       [2.3] Duyệt các cặp giá trị tseg1 (1..16) và tseg2 (1..8):
 *             [2.3.1] Nếu (1 + tseg1 + tseg2) == tq_total:
 *                 - Tính sample point thực tế: actual_sp = (1 + tseg1) / tq_total
 *                 - Chuyển actual_sp thành fixed-point: actual_sp_scaled = actual_sp * 1000
 *                 - So sánh sai số với desired_sample_point_scaled
 *                 - Nếu sai số nhỏ hơn trước đó → lưu lại cấu hình tốt nhất
 *   [3] Trả về cấu hình có sai số nhỏ nhất với sample point mong muốn
 */
CAN_TimingConfig find_best_timing(uint32_t baudrate, uint16_t desired_sample_point_scaled)
{
    CAN_TimingConfig best_config = {0};
    best_config.sample_point_error = FLT_MAX;

    for (int tq_total = 8; tq_total <= 25; tq_total++) {
        if ((HAL_RCC_GetPCLK1Freq() / baudrate) % tq_total != 0)
            continue;

        int prescaler = (HAL_RCC_GetPCLK1Freq() / baudrate) / tq_total;

        for (int tseg1 = 1; tseg1 <= 16; tseg1++) {
            for (int tseg2 = 1; tseg2 <= 8; tseg2++) {
                if (1 + tseg1 + tseg2 != tq_total)
                    continue;

                float actual_sp = (1.0f + tseg1) / tq_total;
                uint16_t actual_sp_scaled = (uint16_t)(actual_sp * SAMPLE_POINT_SCALE);

                float error = fabsf((float)(actual_sp_scaled - desired_sample_point_scaled) / SAMPLE_POINT_SCALE);

                if (error < best_config.sample_point_error) {
                    best_config.tq_total = tq_total;
                    best_config.prescaler = prescaler;
                    best_config.tseg1 = tseg1;
                    best_config.tseg2 = tseg2;
                    best_config.actual_sample_point = actual_sp;
                    best_config.sample_point_error = error;
                }
            }
        }
    }

    return best_config;
}


uint8_t SendCanMessage(uint8_t *data){
	uint32_t id = (data[1]<< 24) |(data[2]<< 16) |(data[3]<< 8) | data[4];
	CanTx_init(id, data[5], &data[6]);
	return 1;
}

void CanTx_init(uint32_t id, uint8_t DlcAndType, uint8_t *data){
	uint32_t txMailbox;
    switch(DlcAndType & 0x0F){
    case CAN_ID_EXT:
    	TxHeader.IDE = CAN_ID_EXT;
    	TxHeader.ExtId = id;
    	break;
    case CAN_ID_STD:
    	TxHeader.IDE = CAN_ID_STD;
    	TxHeader.StdId = id;
    	break;
    }
	TxHeader.RTR = CAN_RTR_DATA;
	TxHeader.DLC = (DlcAndType >> 4);
	TxHeader.TransmitGlobalTime = DISABLE;
	HAL_CAN_AddTxMessage(&hcan1, &TxHeader, data, &txMailbox);
}

void CanRx_init(void){

		sFilterConfig.FilterBank = 0;
		sFilterConfig.FilterMode = CAN_FILTERMODE_IDMASK;
		sFilterConfig.FilterScale = CAN_FILTERSCALE_32BIT;
		sFilterConfig.FilterIdHigh = 0x0000;
		sFilterConfig.FilterIdLow = 0;
		sFilterConfig.FilterMaskIdHigh = 0x0000;
		sFilterConfig.FilterMaskIdLow = 0;
		sFilterConfig.FilterFIFOAssignment = CAN_RX_FIFO0;
		sFilterConfig.FilterActivation = ENABLE;
		HAL_CAN_ConfigFilter(&hcan1, &sFilterConfig);
		HAL_CAN_ActivateNotification(&hcan1,CAN_IT_RX_FIFO0_MSG_PENDING);
}
void CanRx_FilterRange(uint32_t start_id, uint32_t end_id, uint8_t is_extended)
{
    uint32_t range = end_id - start_id + 1;

    // Kiểm tra range có phải là lũy thừa của 2
    if ((range & (range - 1)) != 0) {
        return; // Không phải lũy thừa của 2
    }

    uint32_t mask, id_filter;

    if (is_extended == 0) {
        // Standard ID (11-bit)
        if (start_id > 0x7FF || end_id > 0x7FF) return;

        mask = 0x7FF & ~(range - 1);

        if ((start_id & ~mask) != 0) return;

        id_filter = start_id << 5;
        mask <<= 5;

        sFilterConfig.FilterIdHigh = (uint16_t)(id_filter);
        sFilterConfig.FilterIdLow  = 0x0000;
        sFilterConfig.FilterMaskIdHigh = (uint16_t)(mask);
        sFilterConfig.FilterMaskIdLow  = 0x0000;
    } else {
        // Extended ID (29-bit)
        if (start_id > 0x1FFFFFFF || end_id > 0x1FFFFFFF) return;

        mask = 0x1FFFFFFF & ~(range - 1);

        if ((start_id & ~mask) != 0) return;

        id_filter = (start_id << 3) | (1 << 2);  // IDE bit = 1 in ID field
        mask = (mask << 3) | (1 << 2);           // Mask includes IDE match

        sFilterConfig.FilterIdHigh = (uint16_t)(id_filter >> 16);
        sFilterConfig.FilterIdLow  = (uint16_t)(id_filter & 0xFFFF);
        sFilterConfig.FilterMaskIdHigh = (uint16_t)(mask >> 16);
        sFilterConfig.FilterMaskIdLow  = (uint16_t)(mask & 0xFFFF);
    }

    sFilterConfig.FilterBank = 0;
    sFilterConfig.FilterMode = CAN_FILTERMODE_IDMASK;
    sFilterConfig.FilterScale = CAN_FILTERSCALE_32BIT;
    sFilterConfig.FilterFIFOAssignment = CAN_RX_FIFO0;
    sFilterConfig.FilterActivation = ENABLE;

    HAL_CAN_ConfigFilter(&hcan1, &sFilterConfig);
    HAL_CAN_ActivateNotification(&hcan1, CAN_IT_RX_FIFO0_MSG_PENDING);
}


/* USER CODE END 0 */

/**
  * @brief  The application entry point.
  * @retval int
  */
int main(void)
{

  /* USER CODE BEGIN 1 */

  /* USER CODE END 1 */

  /* MCU Configuration--------------------------------------------------------*/

  /* Reset of all peripherals, Initializes the Flash interface and the Systick. */
  HAL_Init();

  /* USER CODE BEGIN Init */

  /* USER CODE END Init */

  /* Configure the system clock */
  SystemClock_Config();

  /* USER CODE BEGIN SysInit */

  /* USER CODE END SysInit */

  /* Initialize all configured peripherals */
  MX_GPIO_Init();
  MX_DMA_Init();
  //MX_CAN1_Init();
  MX_USB_OTG_FS_PCD_Init();
  MX_UART5_Init();
  MX_I2C1_Init();
  MX_TIM1_Init();
  MX_TIM2_Init();
  MX_TIM4_Init();
  MX_TIM5_Init();
  /* USER CODE BEGIN 2 */
  MX_USB_DEVICE_Init();


  //__HAL_TIM_GET_COUNTER(&htim5)
  /* USER CODE END 2 */

  /* Infinite loop */
  /* USER CODE BEGIN WHILE */
  while (1)
  {
    /* USER CODE END WHILE */

    /* USER CODE BEGIN 3 */
	  Process_HID_Frames();
//	  if(HID_Frame_Read(&hid_frame_fifo_receive,process_sendframe)){
//	      	HAL_GPIO_TogglePin(GPIOA, GPIO_PIN_6);
//	      	USBD_CUSTOM_HID_SendReport(&hUsbDevice,process_sendframe, HID_FRAME_SIZE);
//	      }

//	      if ((now - last_toggle_time) >= 1000)
//	      {
//	          last_toggle_time = now;
//	          HAL_GPIO_TogglePin(GPIOA, GPIO_PIN_7);
//	      }

//	  if((hUsbDevice.dev_state == USBD_STATE_CONFIGURED) && tx_ok){
//		  hoang[3]+=1;
//		  tx_ok = 0;  // Chặn gửi tiếp cho đến khi callback báo xong
//		  USBD_CUSTOM_HID_SendReport(&hUsbDevice, hoang, sizeof(hoang));
//	  }
//	  HAL_Delay(500);
  }
  /* USER CODE END 3 */
}

/**
  * @brief System Clock Configuration
  * @retval None
  */
void SystemClock_Config(void)
{
  RCC_OscInitTypeDef RCC_OscInitStruct = {0};
  RCC_ClkInitTypeDef RCC_ClkInitStruct = {0};

  /** Configure the main internal regulator output voltage
  */
  __HAL_RCC_PWR_CLK_ENABLE();
  __HAL_PWR_VOLTAGESCALING_CONFIG(PWR_REGULATOR_VOLTAGE_SCALE1);

  /** Initializes the RCC Oscillators according to the specified parameters
  * in the RCC_OscInitTypeDef structure.
  */
  RCC_OscInitStruct.OscillatorType = RCC_OSCILLATORTYPE_HSE;
  RCC_OscInitStruct.HSEState = RCC_HSE_ON;
  RCC_OscInitStruct.PLL.PLLState = RCC_PLL_ON;
  RCC_OscInitStruct.PLL.PLLSource = RCC_PLLSOURCE_HSE;
  RCC_OscInitStruct.PLL.PLLM = 4;
  RCC_OscInitStruct.PLL.PLLN = 168;
  RCC_OscInitStruct.PLL.PLLP = RCC_PLLP_DIV2;
  RCC_OscInitStruct.PLL.PLLQ = 7;
  if (HAL_RCC_OscConfig(&RCC_OscInitStruct) != HAL_OK)
  {
    Error_Handler();
  }

  /** Initializes the CPU, AHB and APB buses clocks
  */
  RCC_ClkInitStruct.ClockType = RCC_CLOCKTYPE_HCLK|RCC_CLOCKTYPE_SYSCLK
                              |RCC_CLOCKTYPE_PCLK1|RCC_CLOCKTYPE_PCLK2;
  RCC_ClkInitStruct.SYSCLKSource = RCC_SYSCLKSOURCE_PLLCLK;
  RCC_ClkInitStruct.AHBCLKDivider = RCC_SYSCLK_DIV1;
  RCC_ClkInitStruct.APB1CLKDivider = RCC_HCLK_DIV4;
  RCC_ClkInitStruct.APB2CLKDivider = RCC_HCLK_DIV2;

  if (HAL_RCC_ClockConfig(&RCC_ClkInitStruct, FLASH_LATENCY_5) != HAL_OK)
  {
    Error_Handler();
  }
}

/* USER CODE BEGIN 4 */

void HAL_CAN_RxFifo0MsgPendingCallback(CAN_HandleTypeDef *hcan)
{
    //memset(usbFrame, 0, sizeof(usbFrame));
    if (HAL_CAN_GetRxMessage(&hcan1, CAN_RX_FIFO0, &RxHeader, &usbFrame[6]) == HAL_OK)
    {
        uint32_t timestemp = __HAL_TIM_GET_COUNTER(&htim5);
      // HAL_GPIO_TogglePin(GPIOA, GPIO_PIN_7); // báo nhận
        // Byte 0: CMD
        usbFrame[0] = 0x03;
        // Byte 1: DLC (4-bit high), FrameType (4-bit low)
        uint8_t dlc = RxHeader.DLC & 0x0F;
        usbFrame[1] = (dlc << 4) | (RxHeader.IDE & 0x0F);

        // Byte 2~5: CAN ID (big-endian)
        uint32_t canId = (RxHeader.IDE == CAN_ID_EXT) ? RxHeader.ExtId : RxHeader.StdId;
        usbFrame[2] = (canId >> 24) & 0xFF;
        usbFrame[3] = (canId >> 16) & 0xFF;
        usbFrame[4] = (canId >> 8) & 0xFF;
        usbFrame[5] = canId & 0xFF;
        usbFrame[14] = (timestemp >> 24) & 0xFF;
        usbFrame[15] = (timestemp >> 16) & 0xFF;
        usbFrame[16]= (timestemp >> 8) & 0xFF;
        usbFrame[17] = (timestemp ) & 0xFF;
        HID_Frame_Write(&hid_frame_fifo_receive,usbFrame);
        //USBD_CUSTOM_HID_SendReport(&hUsbDevice,usbFrame, HID_FRAME_SIZE);
    }
}


/* USER CODE END 4 */

/**
  * @brief  This function is executed in case of error occurrence.
  * @retval None
  */
void Error_Handler(void)
{
  /* USER CODE BEGIN Error_Handler_Debug */
  /* User can add his own implementation to report the HAL error return state */
  __disable_irq();
  while (1)
  {
  }
  /* USER CODE END Error_Handler_Debug */
}

#ifdef  USE_FULL_ASSERT
/**
  * @brief  Reports the name of the source file and the source line number
  *         where the assert_param error has occurred.
  * @param  file: pointer to the source file name
  * @param  line: assert_param error line source number
  * @retval None
  */
void assert_failed(uint8_t *file, uint32_t line)
{
  /* USER CODE BEGIN 6 */
  /* User can add his own implementation to report the file name and line number,
     ex: printf("Wrong parameters value: file %s on line %d\r\n", file, line) */
  /* USER CODE END 6 */
}
#endif /* USE_FULL_ASSERT */
